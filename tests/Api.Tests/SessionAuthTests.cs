using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hackaton.Api.Data;
using Hackaton.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hackaton.Api.Tests;

public class SessionAuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}"[..20] + "@example.com";

    private static string UniqueUsername() => "u" + Guid.NewGuid().ToString("N")[..10];

    private HttpClient RawClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        HandleCookies = false,
    });

    private async Task<(string token, string email, string username)> RegisterAndLogin(CancellationToken ct)
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        var username = UniqueUsername();

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, username, password = "Secret123" },
            ct);
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Secret123" },
            ct);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>(ct);
        return (body.GetProperty("token").GetString()!, email, username);
    }

    [Fact]
    public async Task Me_returns_401_when_no_session_cookie()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = RawClient();

        var response = await client.GetAsync("/api/me", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Me_returns_user_when_session_cookie_valid()
    {
        var ct = TestContext.Current.CancellationToken;
        var (token, email, username) = await RegisterAndLogin(ct);
        var client = RawClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("Cookie", $"session={token}");

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
        body.GetProperty("email").GetString().Should().Be(email.ToLowerInvariant());
        body.GetProperty("username").GetString().Should().Be(username.ToLowerInvariant());
    }

    [Fact]
    public async Task Me_returns_401_when_session_token_is_malformed()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = RawClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("Cookie", "session=not-a-guid");

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_current_session_only()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        var username = UniqueUsername();

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, username, password = "Secret123" },
            ct);
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginA = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Secret123" },
            ct);
        var tokenA = (await loginA.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("token").GetString()!;

        var loginB = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Secret123" },
            ct);
        var tokenB = (await loginB.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("token").GetString()!;

        tokenA.Should().NotBe(tokenB);

        var raw = RawClient();
        var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutReq.Headers.Add("Cookie", $"session={tokenA}");
        var logout = await raw.SendAsync(logoutReq, ct);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meWithA = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        meWithA.Headers.Add("Cookie", $"session={tokenA}");
        (await raw.SendAsync(meWithA, ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var meWithB = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        meWithB.Headers.Add("Cookie", $"session={tokenB}");
        (await raw.SendAsync(meWithB, ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var revoked = await db.Sessions.SingleAsync(s => s.Id == Guid.Parse(tokenA), ct);
        var active = await db.Sessions.SingleAsync(s => s.Id == Guid.Parse(tokenB), ct);
        revoked.RevokedAt.Should().NotBeNull();
        active.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Logout_deletes_cookie()
    {
        var ct = TestContext.Current.CancellationToken;
        var (token, _, _) = await RegisterAndLogin(ct);

        var raw = RawClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("Cookie", $"session={token}");

        var response = await raw.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c =>
            c.StartsWith("session=", StringComparison.Ordinal) &&
            c.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_cookie_has_persistent_30_day_expiry()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, username = UniqueUsername(), password = "Secret123" },
            ct);

        var before = DateTimeOffset.UtcNow;
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Secret123" },
            ct);
        var after = DateTimeOffset.UtcNow;

        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var sessionCookie = cookies!.Single(c => c.StartsWith("session=", StringComparison.Ordinal));
        var expiresPart = sessionCookie.Split(';')
            .Select(p => p.Trim())
            .Single(p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        var expiresValue = expiresPart["expires=".Length..];
        var expiresAt = DateTimeOffset.Parse(expiresValue, System.Globalization.CultureInfo.InvariantCulture);

        expiresAt.Should().BeOnOrAfter(before.AddDays(29));
        expiresAt.Should().BeOnOrBefore(after.AddDays(31));
    }
}
