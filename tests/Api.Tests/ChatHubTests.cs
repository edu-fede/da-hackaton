using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using FluentAssertions;
using Hackaton.Api.Data;
using Hackaton.Api.Messages;
using Hackaton.Api.Presence;
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
    public async Task SendMessage_from_non_member_throws_HubException()
    {
        // Post-fix: members auto-join on OnConnectedAsync so a sender that IS a member can
        // send immediately. This test covers the inverse: a stranger connecting to the Hub
        // and trying to send to a room they were never added to.
        var ct = TestContext.Current.CancellationToken;
        var (ownerHttp, _, _) = await AuthenticatedClientAsync(ct);
        var (_, strangerCookie, _) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAndAddMemberAsync(ownerHttp, joinerId: null, ct);

        await using var sender = BuildHubConnection(strangerCookie);
        await sender.StartAsync(ct);

        var act = async () => await sender.InvokeAsync<MessageBroadcast>(
            "SendMessage", roomId, "hello", (Guid?)null, ct);
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.Message.Contains("join", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OnConnected_auto_joins_member_rooms_so_owner_can_send_without_explicit_JoinRoom()
    {
        // Regression for the bug found in Story 1.14 manual smoke: an owner of a newly-created
        // room is in RoomMember in the DB but was never added to the SignalR group because the
        // client-side JoinRoom call races with the user's first SendMessage. Fix: OnConnectedAsync
        // reconciles SignalR groups with the authoritative DB membership. This test covers both
        // sides: the owner can send without calling JoinRoom, AND an existing member receives the
        // broadcast despite NEVER having called JoinRoom either.
        var ct = TestContext.Current.CancellationToken;
        var (ownerHttp, ownerCookie, _) = await AuthenticatedClientAsync(ct);
        var (_, receiverCookie, receiverId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAndAddMemberAsync(ownerHttp, receiverId, ct);

        await using var receiver = BuildHubConnection(receiverCookie);
        var received = new TaskCompletionSource<MessageBroadcast>();
        receiver.On<MessageBroadcast>("MessageReceived", m => received.TrySetResult(m));
        await receiver.StartAsync(ct);

        await using var sender = BuildHubConnection(ownerCookie);
        await sender.StartAsync(ct);

        // No explicit JoinRoom on either side — both must have been auto-joined on connect.
        var ack = await sender.InvokeAsync<MessageBroadcast>(
            "SendMessage", roomId, "first message from owner", (Guid?)null, ct);

        ack.Text.Should().Be("first message from owner");
        ack.RoomId.Should().Be(roomId);

        var broadcast = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        broadcast.Id.Should().Be(ack.Id);
        broadcast.Text.Should().Be("first message from owner");
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
    public async Task OnConnect_broadcasts_PresenceChanged_Online_to_room_peers()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ownerHttp, ownerCookie, _) = await AuthenticatedClientAsync(ct);
        var (_, peerCookie, peerId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAndAddMemberAsync(ownerHttp, peerId, ct);

        await using var owner = BuildHubConnection(ownerCookie);
        var received = new TaskCompletionSource<PresenceBroadcast>();
        owner.On<PresenceBroadcast>("PresenceChanged", p =>
        {
            if (p.UserId == peerId && p.Status == "Online")
            {
                received.TrySetResult(p);
            }
        });
        await owner.StartAsync(ct);

        await using var peer = BuildHubConnection(peerCookie);
        await peer.StartAsync(ct);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        payload.UserId.Should().Be(peerId);
        payload.Status.Should().Be("Online");
    }

    [Fact]
    public async Task OnDisconnect_broadcasts_PresenceChanged_Offline_when_last_connection_closes()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ownerHttp, ownerCookie, _) = await AuthenticatedClientAsync(ct);
        var (_, peerCookie, peerId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAndAddMemberAsync(ownerHttp, peerId, ct);

        await using var owner = BuildHubConnection(ownerCookie);
        var offlineReceived = new TaskCompletionSource<PresenceBroadcast>();
        owner.On<PresenceBroadcast>("PresenceChanged", p =>
        {
            if (p.UserId == peerId && p.Status == "Offline")
            {
                offlineReceived.TrySetResult(p);
            }
        });
        await owner.StartAsync(ct);

        var peer = BuildHubConnection(peerCookie);
        await peer.StartAsync(ct);
        await peer.StopAsync(ct);
        await peer.DisposeAsync();

        var payload = await offlineReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        payload.UserId.Should().Be(peerId);
        payload.Status.Should().Be("Offline");
    }

    [Fact]
    public async Task Heartbeat_invocation_on_connected_user_does_not_throw()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, cookie, _) = await AuthenticatedClientAsync(ct);

        await using var connection = BuildHubConnection(cookie);
        await connection.StartAsync(ct);

        var act = async () => await connection.InvokeAsync("Heartbeat", ct);
        await act.Should().NotThrowAsync();
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

    [Fact]
    public async Task Edit_message_broadcasts_MessageEdited_to_room_group()
    {
        var ct = TestContext.Current.CancellationToken;
        var (authorHttp, authorCookie, authorId) = await AuthenticatedClientAsync(ct);
        var (_, receiverCookie, receiverId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAndAddMemberAsync(authorHttp, receiverId, ct);

        Guid messageId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            messageId = Guid.NewGuid();
            db.Messages.Add(new Message
            {
                Id = messageId,
                RoomId = roomId,
                SenderId = authorId,
                Text = "original",
                CreatedAt = DateTimeOffset.UtcNow,
                SequenceInRoom = 1,
            });
            await db.SaveChangesAsync(ct);
        }

        await using var receiver = BuildHubConnection(receiverCookie);
        var received = new TaskCompletionSource<MessageEditedBroadcast>();
        receiver.On<MessageEditedBroadcast>("MessageEdited", p => received.TrySetResult(p));
        await receiver.StartAsync(ct);

        var response = await authorHttp.PatchAsJsonAsync(
            $"/api/rooms/{roomId}/messages/{messageId}",
            new { text = "revised" },
            ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        payload.Id.Should().Be(messageId);
        payload.RoomId.Should().Be(roomId);
        payload.Text.Should().Be("revised");
    }

    [Fact]
    public async Task Delete_message_broadcasts_MessageDeleted_to_room_group()
    {
        var ct = TestContext.Current.CancellationToken;
        var (authorHttp, authorCookie, authorId) = await AuthenticatedClientAsync(ct);
        var (_, receiverCookie, receiverId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAndAddMemberAsync(authorHttp, receiverId, ct);

        Guid messageId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            messageId = Guid.NewGuid();
            db.Messages.Add(new Message
            {
                Id = messageId,
                RoomId = roomId,
                SenderId = authorId,
                Text = "original",
                CreatedAt = DateTimeOffset.UtcNow,
                SequenceInRoom = 1,
            });
            await db.SaveChangesAsync(ct);
        }

        await using var receiver = BuildHubConnection(receiverCookie);
        var received = new TaskCompletionSource<MessageDeletedBroadcast>();
        receiver.On<MessageDeletedBroadcast>("MessageDeleted", p => received.TrySetResult(p));
        await receiver.StartAsync(ct);

        var response = await authorHttp.DeleteAsync($"/api/rooms/{roomId}/messages/{messageId}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        payload.Id.Should().Be(messageId);
        payload.RoomId.Should().Be(roomId);
        payload.DeletedAt.Should().NotBe(default(DateTimeOffset));
    }
}
