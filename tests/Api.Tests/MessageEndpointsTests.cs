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

    // ----- PATCH /api/rooms/{id}/messages/{messageId} -----

    private async Task<Guid> SeedOneMessageAsync(Guid roomId, Guid senderId, int sequenceInRoom, CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        db.Messages.Add(new Message
        {
            Id = id,
            RoomId = roomId,
            SenderId = senderId,
            Text = "original text",
            CreatedAt = DateTimeOffset.UtcNow,
            SequenceInRoom = sequenceInRoom,
        });
        await db.SaveChangesAsync(ct);
        return id;
    }

    [Fact]
    public async Task Edit_message_by_author_returns_200_and_sets_EditedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        var messageId = await SeedOneMessageAsync(roomId, userId, 1, ct);

        var response = await client.PatchAsJsonAsync(
            $"/api/rooms/{roomId}/messages/{messageId}",
            new { text = "edited text" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("text").GetString().Should().Be("edited text");
        body.GetProperty("editedAt").ValueKind.Should().NotBe(JsonValueKind.Null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.Messages.SingleAsync(m => m.Id == messageId, ct);
        persisted.Text.Should().Be("edited text");
        persisted.EditedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Edit_message_by_non_author_returns_403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, ownerId) = await AuthenticatedClientAsync(ct);
        var (other, otherId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(owner, ct);
        (await other.PostAsync($"/api/rooms/{roomId}/join", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var messageId = await SeedOneMessageAsync(roomId, ownerId, 1, ct);

        var response = await other.PatchAsJsonAsync(
            $"/api/rooms/{roomId}/messages/{messageId}",
            new { text = "hijack" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _ = otherId; // silence unused
    }

    [Fact]
    public async Task Edit_message_by_non_member_returns_403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, ownerId) = await AuthenticatedClientAsync(ct);
        var (stranger, _) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(owner, ct);
        var messageId = await SeedOneMessageAsync(roomId, ownerId, 1, ct);

        var response = await stranger.PatchAsJsonAsync(
            $"/api/rooms/{roomId}/messages/{messageId}",
            new { text = "hijack" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Edit_already_deleted_message_returns_410()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        var messageId = await SeedOneMessageAsync(roomId, userId, 1, ct);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var m = await db.Messages.SingleAsync(m => m.Id == messageId, ct);
            m.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var response = await client.PatchAsJsonAsync(
            $"/api/rooms/{roomId}/messages/{messageId}",
            new { text = "too late" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task Edit_empty_text_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        var messageId = await SeedOneMessageAsync(roomId, userId, 1, ct);

        var response = await client.PatchAsJsonAsync(
            $"/api/rooms/{roomId}/messages/{messageId}",
            new { text = "" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Edit_message_text_with_only_whitespace_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        var messageId = await SeedOneMessageAsync(roomId, userId, 1, ct);

        var response = await client.PatchAsJsonAsync(
            $"/api/rooms/{roomId}/messages/{messageId}",
            new { text = "   \t\n  " },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Edit_text_over_3KB_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        var messageId = await SeedOneMessageAsync(roomId, userId, 1, ct);
        var tooLong = new string('x', 4 * 1024);

        var response = await client.PatchAsJsonAsync(
            $"/api/rooms/{roomId}/messages/{messageId}",
            new { text = tooLong },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Edit_nonexistent_message_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);

        var response = await client.PatchAsJsonAsync(
            $"/api/rooms/{roomId}/messages/{Guid.NewGuid()}",
            new { text = "nope" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----- DELETE /api/rooms/{id}/messages/{messageId} -----

    [Fact]
    public async Task Delete_message_by_author_returns_204_and_sets_DeletedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        var messageId = await SeedOneMessageAsync(roomId, userId, 1, ct);

        var response = await client.DeleteAsync($"/api/rooms/{roomId}/messages/{messageId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.Messages.SingleAsync(m => m.Id == messageId, ct);
        persisted.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_message_by_admin_returns_204()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, _) = await AuthenticatedClientAsync(ct);
        var (author, authorId) = await AuthenticatedClientAsync(ct);
        var (admin, adminId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(owner, ct);
        (await author.PostAsync($"/api/rooms/{roomId}/join", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsync($"/api/rooms/{roomId}/join", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var m = await db.RoomMembers.SingleAsync(x => x.RoomId == roomId && x.UserId == adminId, ct);
            m.Role = RoomRole.Admin;
            await db.SaveChangesAsync(ct);
        }

        var messageId = await SeedOneMessageAsync(roomId, authorId, 1, ct);

        var response = await admin.DeleteAsync($"/api/rooms/{roomId}/messages/{messageId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_message_by_owner_returns_204()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, _) = await AuthenticatedClientAsync(ct);
        var (author, authorId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(owner, ct);
        (await author.PostAsync($"/api/rooms/{roomId}/join", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var messageId = await SeedOneMessageAsync(roomId, authorId, 1, ct);

        var response = await owner.DeleteAsync($"/api/rooms/{roomId}/messages/{messageId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_message_by_other_member_returns_403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, ownerId) = await AuthenticatedClientAsync(ct);
        var (other, _) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(owner, ct);
        (await other.PostAsync($"/api/rooms/{roomId}/join", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var messageId = await SeedOneMessageAsync(roomId, ownerId, 1, ct);

        var response = await other.DeleteAsync($"/api/rooms/{roomId}/messages/{messageId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_already_deleted_message_is_idempotent_204()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);
        var messageId = await SeedOneMessageAsync(roomId, userId, 1, ct);

        (await client.DeleteAsync($"/api/rooms/{roomId}/messages/{messageId}", ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var second = await client.DeleteAsync($"/api/rooms/{roomId}/messages/{messageId}", ct);

        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_nonexistent_message_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await AuthenticatedClientAsync(ct);
        var roomId = await CreateRoomAsync(client, ct);

        var response = await client.DeleteAsync($"/api/rooms/{roomId}/messages/{Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
