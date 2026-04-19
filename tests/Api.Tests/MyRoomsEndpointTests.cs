using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hackaton.Api.Tests.Fixtures;
using Xunit;

namespace Hackaton.Api.Tests;

public class MyRoomsEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}"[..20] + "@example.com";

    private static string UniqueUsername() => "u" + Guid.NewGuid().ToString("N")[..10];

    private static string UniqueRoomName(string prefix = "mine") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<HttpClient> AuthenticatedClientAsync(CancellationToken ct)
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        var password = "Secret123";
        await client.PostAsJsonAsync("/api/auth/register", new { email, username = UniqueUsername(), password }, ct);
        await client.PostAsJsonAsync("/api/auth/login", new { email, password }, ct);
        return client;
    }

    [Fact]
    public async Task My_rooms_returns_membership_with_role_and_memberCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await AuthenticatedClientAsync(ct);
        var name = UniqueRoomName();

        var created = await owner.PostAsJsonAsync(
            "/api/rooms",
            new { name, description = "my creation", visibility = "Private" },
            ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var roomId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();

        var response = await owner.GetFromJsonAsync<JsonElement>("/api/me/rooms", ct);
        var entries = response.EnumerateArray().ToList();

        var entry = entries.Single(e => e.GetProperty("id").GetGuid() == roomId);
        entry.GetProperty("name").GetString().Should().Be(name);
        entry.GetProperty("visibility").GetString().Should().Be("Private");
        entry.GetProperty("role").GetString().Should().Be("Owner");
        entry.GetProperty("memberCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task My_rooms_excludes_rooms_where_caller_is_not_a_member()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await AuthenticatedClientAsync(ct);
        var stranger = await AuthenticatedClientAsync(ct);
        var name = UniqueRoomName("private");

        var created = await owner.PostAsJsonAsync(
            "/api/rooms",
            new { name, description = "owner only", visibility = "Private" },
            ct);
        var roomId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();

        var response = await stranger.GetFromJsonAsync<JsonElement>("/api/me/rooms", ct);
        var ids = response.EnumerateArray()
            .Select(e => e.GetProperty("id").GetGuid())
            .ToList();

        ids.Should().NotContain(roomId);
    }

    [Fact]
    public async Task My_rooms_requires_authentication()
    {
        var ct = TestContext.Current.CancellationToken;
        var anon = _factory.CreateClient();

        var response = await anon.GetAsync("/api/me/rooms", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
