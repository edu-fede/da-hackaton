using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hackaton.Api.Data;
using Hackaton.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hackaton.Api.Tests;

public class ResyncEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}"[..20] + "@example.com";

    private static string UniqueUsername() => "u" + Guid.NewGuid().ToString("N")[..10];

    private static string UniqueRoomName(string prefix = "sync") => $"{prefix}-{Guid.NewGuid():N}"[..20];

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
            new { name = UniqueRoomName(), description = "resync test", visibility = "Public" },
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
            Text = $"seed {i}",
            CreatedAt = baseTime.AddSeconds(i),
            SequenceInRoom = i,
        });
        db.Messages.AddRange(messages);
        await db.SaveChangesAsync(ct);
    }

    private async Task RemoveMembershipAsync(Guid roomId, Guid userId, CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.RoomMembers.SingleAsync(m => m.RoomId == roomId && m.UserId == userId, ct);
        db.RoomMembers.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task Resync_returns_missing_messages_above_lastSeq_in_ascending_order()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        await SeedMessagesAsync(roomId, userId, count: 10, ct);

        var response = await client.PostAsJsonAsync(
            "/api/rooms/resync",
            new[] { new { roomId, lastSeq = 3 } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var entries = body.EnumerateArray().ToList();
        entries.Should().HaveCount(1);

        var result = entries[0];
        result.GetProperty("notAMember").GetBoolean().Should().BeFalse();
        var messages = result.GetProperty("messages").EnumerateArray().ToList();
        messages.Should().HaveCount(7);
        messages.Select(m => m.GetProperty("sequenceInRoom").GetInt32())
            .Should().Equal(4, 5, 6, 7, 8, 9, 10);
    }

    [Fact]
    public async Task Resync_returns_empty_when_lastSeq_equals_latest()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        await SeedMessagesAsync(roomId, userId, count: 5, ct);

        var response = await client.PostAsJsonAsync(
            "/api/rooms/resync",
            new[] { new { roomId, lastSeq = 5 } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var result = body.EnumerateArray().Single();
        result.GetProperty("notAMember").GetBoolean().Should().BeFalse();
        result.GetProperty("messages").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Resync_returns_notAMember_for_unknown_room()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await AuthenticatedClientAsync(ct);
        var ghostRoom = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            "/api/rooms/resync",
            new[] { new { roomId = ghostRoom, lastSeq = 0 } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var result = body.EnumerateArray().Single();
        result.GetProperty("roomId").GetGuid().Should().Be(ghostRoom);
        result.GetProperty("notAMember").GetBoolean().Should().BeTrue();
        result.GetProperty("messages").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Resync_returns_notAMember_when_caller_removed_from_room()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        await SeedMessagesAsync(roomId, userId, count: 3, ct);
        await RemoveMembershipAsync(roomId, userId, ct);

        var response = await client.PostAsJsonAsync(
            "/api/rooms/resync",
            new[] { new { roomId, lastSeq = 0 } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var result = body.EnumerateArray().Single();
        result.GetProperty("notAMember").GetBoolean().Should().BeTrue();
        result.GetProperty("messages").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Resync_caps_per_room_messages_at_500()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        await SeedMessagesAsync(roomId, userId, count: 600, ct);

        var response = await client.PostAsJsonAsync(
            "/api/rooms/resync",
            new[] { new { roomId, lastSeq = 0 } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var messages = body.EnumerateArray().Single().GetProperty("messages").EnumerateArray().ToList();

        messages.Should().HaveCount(500);
        messages.First().GetProperty("sequenceInRoom").GetInt32().Should().Be(1);
        messages.Last().GetProperty("sequenceInRoom").GetInt32().Should().Be(500);
    }

    [Fact]
    public async Task Resync_rejects_anonymous_with_401()
    {
        var ct = TestContext.Current.CancellationToken;
        var anon = _factory.CreateClient();

        var response = await anon.PostAsJsonAsync(
            "/api/rooms/resync",
            new[] { new { roomId = Guid.NewGuid(), lastSeq = 0 } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Resync_handles_mixed_rooms_in_one_call()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var memberRoom = await CreateRoomAsync(client, ct);
        await SeedMessagesAsync(memberRoom, userId, count: 2, ct);
        var ghostRoom = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            "/api/rooms/resync",
            new object[]
            {
                new { roomId = memberRoom, lastSeq = 0 },
                new { roomId = ghostRoom, lastSeq = 0 },
            },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var entries = body.EnumerateArray().ToList();
        entries.Should().HaveCount(2);
        entries[0].GetProperty("roomId").GetGuid().Should().Be(memberRoom);
        entries[0].GetProperty("notAMember").GetBoolean().Should().BeFalse();
        entries[0].GetProperty("messages").EnumerateArray().Should().HaveCount(2);
        entries[1].GetProperty("roomId").GetGuid().Should().Be(ghostRoom);
        entries[1].GetProperty("notAMember").GetBoolean().Should().BeTrue();
    }
}
