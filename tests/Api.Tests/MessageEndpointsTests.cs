using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hackaton.Api.Data;
using Hackaton.Api.Messages;
using Hackaton.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hackaton.Api.Tests;

public class MessageEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}"[..20] + "@example.com";

    private static string UniqueUsername() => "u" + Guid.NewGuid().ToString("N")[..10];

    private static string UniqueRoomName(string prefix = "msgs") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<(HttpClient client, Guid userId)> AuthenticatedClientAsync(CancellationToken ct)
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", new { email, username = UniqueUsername(), password = "Secret123" }, ct);
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Secret123" }, ct);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me", ct);
        return (client, me.GetProperty("id").GetGuid());
    }

    private async Task<Guid> CreateRoomAsync(HttpClient client, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(
            "/api/rooms",
            new { name = UniqueRoomName(), description = "msg history", visibility = "Public" },
            ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return body.GetProperty("id").GetGuid();
    }

    private async Task SeedMessagesAsync(Guid roomId, Guid senderId, int count, CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-count);
        var messages = Enumerable.Range(1, count).Select(i => new Message
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            SenderId = senderId,
            Text = $"message {i}",
            CreatedAt = baseTime.AddSeconds(i),
            SequenceInRoom = i,
        });
        db.Messages.AddRange(messages);
        await db.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task Get_messages_returns_latest_page_ordered_by_sequence_desc()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        await SeedMessagesAsync(roomId, userId, 100, ct);

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/rooms/{roomId}/messages", ct);
        var entries = body.EnumerateArray().ToList();

        entries.Should().HaveCount(50);
        entries[0].GetProperty("sequenceInRoom").GetInt32().Should().Be(100);
        entries[49].GetProperty("sequenceInRoom").GetInt32().Should().Be(51);
    }

    [Fact]
    public async Task Get_messages_pagination_via_beforeSeq()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        await SeedMessagesAsync(roomId, userId, 150, ct);

        var first = await client.GetFromJsonAsync<JsonElement>($"/api/rooms/{roomId}/messages", ct);
        var firstSeqs = first.EnumerateArray().Select(e => e.GetProperty("sequenceInRoom").GetInt32()).ToList();
        firstSeqs.Should().HaveCount(50);
        var minOfFirst = firstSeqs.Min();

        var second = await client.GetFromJsonAsync<JsonElement>(
            $"/api/rooms/{roomId}/messages?beforeSeq={minOfFirst}",
            ct);
        var secondSeqs = second.EnumerateArray().Select(e => e.GetProperty("sequenceInRoom").GetInt32()).ToList();
        secondSeqs.Should().HaveCount(50);
        secondSeqs.Max().Should().BeLessThan(minOfFirst);
        secondSeqs.Intersect(firstSeqs).Should().BeEmpty();
    }

    [Fact]
    public async Task Get_messages_limit_is_capped_at_100()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        await SeedMessagesAsync(roomId, userId, 250, ct);

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/rooms/{roomId}/messages?limit=1000", ct);
        body.EnumerateArray().Count().Should().Be(100);
    }

    [Fact]
    public async Task Get_messages_non_member_returns_403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, _) = await AuthenticatedClientAsync(ct);
        var (stranger, _) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(owner, ct);

        var response = await stranger.GetAsync($"/api/rooms/{roomId}/messages", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_messages_room_not_found_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await AuthenticatedClientAsync(ct);

        var response = await client.GetAsync($"/api/rooms/{Guid.NewGuid()}/messages", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_messages_anonymous_returns_401()
    {
        var ct = TestContext.Current.CancellationToken;
        var anon = _factory.CreateClient();

        var response = await anon.GetAsync($"/api/rooms/{Guid.NewGuid()}/messages", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_messages_includes_sender_username_and_soft_delete_marker()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Messages.Add(new Message
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                SenderId = userId,
                Text = "live one",
                CreatedAt = DateTimeOffset.UtcNow,
                SequenceInRoom = 1,
            });
            db.Messages.Add(new Message
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                SenderId = userId,
                Text = "you shouldn't see this",
                CreatedAt = DateTimeOffset.UtcNow,
                DeletedAt = DateTimeOffset.UtcNow,
                SequenceInRoom = 2,
            });
            await db.SaveChangesAsync(ct);
        }

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/rooms/{roomId}/messages", ct);
        var entries = body.EnumerateArray().ToList();

        entries.Should().HaveCount(2);
        var deleted = entries.Single(e => e.GetProperty("sequenceInRoom").GetInt32() == 2);
        deleted.GetProperty("text").ValueKind.Should().Be(JsonValueKind.Null);
        deleted.GetProperty("deletedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        deleted.GetProperty("senderUsername").GetString().Should().NotBeNullOrEmpty();

        var live = entries.Single(e => e.GetProperty("sequenceInRoom").GetInt32() == 1);
        live.GetProperty("text").GetString().Should().Be("live one");
        live.GetProperty("deletedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Get_messages_returns_latest_page_under_200ms_with_10K_messages()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        await SeedMessagesAsync(roomId, userId, 10_000, ct);

        // Warm up connection pool + query plan cache.
        _ = await client.GetFromJsonAsync<JsonElement>($"/api/rooms/{roomId}/messages?limit=1", ct);

        var sw = Stopwatch.StartNew();
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/rooms/{roomId}/messages", ct);
        sw.Stop();

        body.EnumerateArray().Count().Should().Be(50);
        sw.ElapsedMilliseconds.Should().BeLessThan(200, "NFR-6 — cursor pagination should stay snappy at 10K messages");
    }
}
