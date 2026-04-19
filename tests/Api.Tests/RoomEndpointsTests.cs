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

public class RoomEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}"[..20] + "@example.com";

    private static string UniqueUsername() => "u" + Guid.NewGuid().ToString("N")[..10];

    private static string UniqueRoomName(string prefix = "room") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<HttpClient> AuthenticatedClientAsync(CancellationToken ct)
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        var password = "Secret123";
        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, username = UniqueUsername(), password },
            ct);
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password },
            ct);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    private async Task<Guid> CurrentUserIdAsync(HttpClient client, CancellationToken ct)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/me", ct);
        return response.GetProperty("id").GetGuid();
    }

    // ----- POST /api/rooms -----

    [Fact]
    public async Task Create_room_returns_201_and_persists_owner_membership()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await AuthenticatedClientAsync(ct);
        var userId = await CurrentUserIdAsync(client, ct);
        var name = UniqueRoomName();

        var response = await client.PostAsJsonAsync(
            "/api/rooms",
            new { name, description = "hello", visibility = "Public" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var roomId = body.GetProperty("id").GetGuid();
        body.GetProperty("name").GetString().Should().Be(name);
        body.GetProperty("memberCount").GetInt32().Should().Be(1);
        body.GetProperty("visibility").GetString().Should().Be("Public");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var room = await db.Rooms.SingleAsync(r => r.Id == roomId, ct);
        room.OwnerId.Should().Be(userId);
        var ownership = await db.RoomMembers.SingleAsync(m => m.RoomId == roomId && m.UserId == userId, ct);
        ownership.Role.Should().Be(RoomRole.Owner);
    }

    [Fact]
    public async Task Create_room_duplicate_name_returns_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var alice = await AuthenticatedClientAsync(ct);
        var bob = await AuthenticatedClientAsync(ct);
        var name = UniqueRoomName("dup");

        var first = await alice.PostAsJsonAsync(
            "/api/rooms",
            new { name, description = "first", visibility = "Public" },
            ct);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await bob.PostAsJsonAsync(
            "/api/rooms",
            new { name, description = "second", visibility = "Public" },
            ct);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        second.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Create_room_anonymous_returns_401()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/rooms",
            new { name = UniqueRoomName(), description = "anon", visibility = "Public" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ----- GET /api/rooms -----

    [Fact]
    public async Task List_rooms_returns_only_public_non_deleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await AuthenticatedClientAsync(ct);
        var publicName = UniqueRoomName("pub");
        var privateName = UniqueRoomName("prv");
        var deletedName = UniqueRoomName("del");

        await client.PostAsJsonAsync("/api/rooms", new { name = publicName, description = "", visibility = "Public" }, ct);
        await client.PostAsJsonAsync("/api/rooms", new { name = privateName, description = "", visibility = "Private" }, ct);
        var deletedResp = await client.PostAsJsonAsync("/api/rooms", new { name = deletedName, description = "", visibility = "Public" }, ct);
        var deletedId = (await deletedResp.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var room = await db.Rooms.SingleAsync(r => r.Id == deletedId, ct);
            room.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var list = await client.GetFromJsonAsync<JsonElement>("/api/rooms", ct);
        var names = list.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();

        names.Should().Contain(publicName);
        names.Should().NotContain(privateName);
        names.Should().NotContain(deletedName);
    }

    [Fact]
    public async Task List_rooms_filters_by_q_case_insensitively()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await AuthenticatedClientAsync(ct);
        var marker = Guid.NewGuid().ToString("N")[..6];
        var matchingName = $"discuss-{marker}";
        var descriptionMatcher = UniqueRoomName("plain");

        await client.PostAsJsonAsync("/api/rooms", new { name = matchingName, description = "general", visibility = "Public" }, ct);
        await client.PostAsJsonAsync("/api/rooms", new { name = descriptionMatcher, description = $"about {marker} topic", visibility = "Public" }, ct);
        await client.PostAsJsonAsync("/api/rooms", new { name = UniqueRoomName("other"), description = "unrelated", visibility = "Public" }, ct);

        var list = await client.GetFromJsonAsync<JsonElement>(
            $"/api/rooms?q={marker.ToUpperInvariant()}",
            ct);
        var names = list.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();

        names.Should().Contain(matchingName);
        names.Should().Contain(descriptionMatcher);
    }

    [Fact]
    public async Task List_rooms_includes_member_count()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await AuthenticatedClientAsync(ct);
        var joinerA = await AuthenticatedClientAsync(ct);
        var joinerB = await AuthenticatedClientAsync(ct);
        var name = UniqueRoomName("count");

        var created = await owner.PostAsJsonAsync("/api/rooms", new { name, description = "", visibility = "Public" }, ct);
        var roomId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();

        (await joinerA.PostAsync($"/api/rooms/{roomId}/join", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await joinerB.PostAsync($"/api/rooms/{roomId}/join", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await owner.GetFromJsonAsync<JsonElement>($"/api/rooms?q={name}", ct);
        var entry = list.EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == roomId);
        entry.GetProperty("memberCount").GetInt32().Should().Be(3);
    }

    // ----- POST /api/rooms/{id}/join -----

    [Fact]
    public async Task Join_adds_membership_and_returns_204()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await AuthenticatedClientAsync(ct);
        var joiner = await AuthenticatedClientAsync(ct);
        var joinerId = await CurrentUserIdAsync(joiner, ct);

        var created = await owner.PostAsJsonAsync("/api/rooms", new { name = UniqueRoomName("join"), description = "", visibility = "Public" }, ct);
        var roomId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();

        var response = await joiner.PostAsync($"/api/rooms/{roomId}/join", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = await db.RoomMembers.SingleAsync(m => m.RoomId == roomId && m.UserId == joinerId, ct);
        membership.Role.Should().Be(RoomRole.Member);
    }

    [Fact]
    public async Task Join_banned_user_returns_403()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await AuthenticatedClientAsync(ct);
        var ownerId = await CurrentUserIdAsync(owner, ct);
        var banned = await AuthenticatedClientAsync(ct);
        var bannedId = await CurrentUserIdAsync(banned, ct);

        var created = await owner.PostAsJsonAsync("/api/rooms", new { name = UniqueRoomName("ban"), description = "", visibility = "Public" }, ct);
        var roomId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RoomBans.Add(new RoomBan
            {
                RoomId = roomId,
                UserId = bannedId,
                BannedByUserId = ownerId,
                BannedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }

        var response = await banned.PostAsync($"/api/rooms/{roomId}/join", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Join_private_room_returns_403()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await AuthenticatedClientAsync(ct);
        var stranger = await AuthenticatedClientAsync(ct);

        var created = await owner.PostAsJsonAsync("/api/rooms", new { name = UniqueRoomName("priv"), description = "", visibility = "Private" }, ct);
        var roomId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();

        var response = await stranger.PostAsync($"/api/rooms/{roomId}/join", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Join_nonexistent_room_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await AuthenticatedClientAsync(ct);

        var response = await client.PostAsync($"/api/rooms/{Guid.NewGuid()}/join", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----- POST /api/rooms/{id}/leave -----

    [Fact]
    public async Task Leave_removes_membership_and_returns_204()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await AuthenticatedClientAsync(ct);
        var joiner = await AuthenticatedClientAsync(ct);
        var joinerId = await CurrentUserIdAsync(joiner, ct);

        var created = await owner.PostAsJsonAsync("/api/rooms", new { name = UniqueRoomName("lv"), description = "", visibility = "Public" }, ct);
        var roomId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();
        (await joiner.PostAsync($"/api/rooms/{roomId}/join", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await joiner.PostAsync($"/api/rooms/{roomId}/leave", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.UserId == joinerId, ct);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Owner_cannot_leave_own_room_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await AuthenticatedClientAsync(ct);

        var created = await owner.PostAsJsonAsync("/api/rooms", new { name = UniqueRoomName("own"), description = "", visibility = "Public" }, ct);
        var roomId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();

        var response = await owner.PostAsync($"/api/rooms/{roomId}/leave", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Leave_nonmember_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await AuthenticatedClientAsync(ct);
        var stranger = await AuthenticatedClientAsync(ct);

        var created = await owner.PostAsJsonAsync("/api/rooms", new { name = UniqueRoomName("nomem"), description = "", visibility = "Public" }, ct);
        var roomId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();

        var response = await stranger.PostAsync($"/api/rooms/{roomId}/leave", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
