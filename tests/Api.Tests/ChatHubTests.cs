using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using FluentAssertions;
using Hackaton.Api.Data;
using Hackaton.Api.Messages;
using Hackaton.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hackaton.Api.Tests;

public class ChatHubTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}"[..20] + "@example.com";

    private static string UniqueUsername() => "u" + Guid.NewGuid().ToString("N")[..10];

    private static string UniqueRoomName() => $"hub-{Guid.NewGuid():N}"[..20];

    private async Task<(HttpClient client, string cookie, Guid userId)> AuthenticatedClientAsync(CancellationToken ct)
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", new { email, username = UniqueUsername(), password = "Secret123" }, ct);
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Secret123" }, ct);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var token = body.GetProperty("token").GetString()!;
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me", ct);
        return (client, $"session={token}", me.GetProperty("id").GetGuid());
    }

    private HubConnection BuildHubConnection(string sessionCookie, HttpTransportType transport = HttpTransportType.LongPolling)
    {
        var server = _factory.Server;
        var handler = server.CreateHandler();
        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = transport;
                options.Headers["Cookie"] = sessionCookie;
                if (transport == HttpTransportType.WebSockets)
                {
                    options.SkipNegotiation = true;
                    options.WebSocketFactory = async (wsContext, ct) =>
                    {
                        var wsClient = server.CreateWebSocketClient();
                        wsClient.ConfigureRequest = r => r.Headers["Cookie"] = sessionCookie;
                        return await wsClient.ConnectAsync(wsContext.Uri, ct);
                    };
                }
            })
            .Build();
    }

    private async Task<Guid> CreateRoomAndAddMemberAsync(HttpClient owner, Guid? joinerId, CancellationToken ct)
    {
        var created = await owner.PostAsJsonAsync(
            "/api/rooms",
            new { name = UniqueRoomName(), description = "hub", visibility = "Public" },
            ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var roomId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();

        if (joinerId is Guid uid)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RoomMembers.Add(new RoomMember
            {
                RoomId = roomId,
                UserId = uid,
                Role = RoomRole.Member,
                JoinedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }

        return roomId;
    }

    [Fact]
    public async Task SendMessage_broadcasts_to_room_group_members()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ownerHttp, ownerCookie, _) = await AuthenticatedClientAsync(ct);
        var (_, receiverCookie, receiverId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAndAddMemberAsync(ownerHttp, receiverId, ct);

        await using var sender = BuildHubConnection(ownerCookie);
        await using var receiver = BuildHubConnection(receiverCookie);

        var received = new TaskCompletionSource<MessageBroadcast>();
        receiver.On<MessageBroadcast>("MessageReceived", broadcast =>
        {
            received.TrySetResult(broadcast);
        });

        await sender.StartAsync(ct);
        await receiver.StartAsync(ct);
        await sender.InvokeAsync("JoinRoom", roomId, ct);
        await receiver.InvokeAsync("JoinRoom", roomId, ct);

        var ack = await sender.InvokeAsync<MessageBroadcast>("SendMessage", roomId, "hello team", (Guid?)null, ct);

        ack.Text.Should().Be("hello team");
        ack.RoomId.Should().Be(roomId);

        var broadcast = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        broadcast.Id.Should().Be(ack.Id);
        broadcast.Text.Should().Be("hello team");
        broadcast.SequenceInRoom.Should().BeNull();
    }

    [Fact]
    public async Task SendMessage_results_in_persisted_row_via_background_consumer()
    {
        // Post-1.12: the queue drains continuously, so we assert at the DB level. The row
        // only appears if (a) the Hub wrote to the channel and (b) the BackgroundService
        // consumed it — the AC3 "writes a MessageWorkItem to the channel" invariant still
        // holds, verified end-to-end.
        var ct = TestContext.Current.CancellationToken;
        var (ownerHttp, ownerCookie, _) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAndAddMemberAsync(ownerHttp, joinerId: null, ct);

        await using var sender = BuildHubConnection(ownerCookie);
        await sender.StartAsync(ct);
        await sender.InvokeAsync("JoinRoom", roomId, ct);
        var ack = await sender.InvokeAsync<MessageBroadcast>("SendMessage", roomId, "for the consumer", (Guid?)null, ct);

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        Data.Message? persisted = null;
        while (DateTimeOffset.UtcNow < deadline && persisted is null)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            persisted = await db.Messages.SingleOrDefaultAsync(m => m.Id == ack.Id, ct);
            if (persisted is null) await Task.Delay(100, ct);
        }

        persisted.Should().NotBeNull("the BackgroundService must persist the queued message");
        persisted!.Text.Should().Be("for the consumer");
        persisted.RoomId.Should().Be(roomId);
        persisted.SequenceInRoom.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task JoinRoom_rejects_non_member_with_HubException()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ownerHttp, _, _) = await AuthenticatedClientAsync(ct);
        var (_, strangerCookie, _) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAndAddMemberAsync(ownerHttp, joinerId: null, ct);

        await using var stranger = BuildHubConnection(strangerCookie);
        await stranger.StartAsync(ct);

        var act = async () => await stranger.InvokeAsync("JoinRoom", roomId, ct);
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.Message.Contains("member", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SendMessage_without_prior_JoinRoom_throws_HubException()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ownerHttp, ownerCookie, _) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAndAddMemberAsync(ownerHttp, joinerId: null, ct);

        await using var sender = BuildHubConnection(ownerCookie);
        await sender.StartAsync(ct);

        var act = async () => await sender.InvokeAsync<MessageBroadcast>(
            "SendMessage", roomId, "hello", (Guid?)null, ct);
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.Message.Contains("join", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SendMessage_rejects_text_over_3KB()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ownerHttp, ownerCookie, _) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAndAddMemberAsync(ownerHttp, joinerId: null, ct);

        await using var sender = BuildHubConnection(ownerCookie);
        await sender.StartAsync(ct);
        await sender.InvokeAsync("JoinRoom", roomId, ct);

        var tooLong = new string('x', 4 * 1024);
        Encoding.UTF8.GetByteCount(tooLong).Should().BeGreaterThan(3 * 1024);

        var act = async () => await sender.InvokeAsync<MessageBroadcast>(
            "SendMessage", roomId, tooLong, (Guid?)null, ct);
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.Message.Contains("3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Hub_requires_authentication()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = _factory.Server.CreateHandler();
        await using var anon = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        var act = async () => await anon.StartAsync(ct);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendMessage_succeeds_with_db_paused()
    {
        // Uses WebSocket transport because auth middleware re-runs per-HTTP-request
        // on LongPolling (hits DB each poll); WebSocket handshakes auth once and then
        // the hub method itself doesn't touch the DB — that's the real AC property.
        var ct = TestContext.Current.CancellationToken;
        var (ownerHttp, ownerCookie, _) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAndAddMemberAsync(ownerHttp, joinerId: null, ct);

        await using var sender = BuildHubConnection(ownerCookie, HttpTransportType.WebSockets);
        await sender.StartAsync(ct);
        await sender.InvokeAsync("JoinRoom", roomId, ct);

        await _factory.PausePostgresAsync();
        try
        {
            var ack = await sender.InvokeAsync<MessageBroadcast>(
                "SendMessage", roomId, "hello while paused", (Guid?)null, ct);
            ack.Text.Should().Be("hello while paused");
            ack.RoomId.Should().Be(roomId);
        }
        finally
        {
            await _factory.UnpausePostgresAsync();
        }
    }
}
