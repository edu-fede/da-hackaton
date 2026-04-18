using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hackaton.Api.Data;
using Hackaton.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hackaton.Api.Tests;

public class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}"[..20] + "@example.com";

    private static string UniqueUsername() => "u" + Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task Register_returns_201_with_user_summary()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        var username = UniqueUsername();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, username, password = "Secret123" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
        body.GetProperty("email").GetString().Should().Be(email.ToLowerInvariant());
        body.GetProperty("username").GetString().Should().Be(username.ToLowerInvariant());
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Register_persists_password_as_hash_not_plaintext()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        var password = "Secret123";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, username = UniqueUsername(), password },
            ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Users.SingleAsync(u => u.Email == email.ToLowerInvariant(), ct);

        stored.PasswordHash.Should().NotContain(password);
        stored.PasswordHash.Should().NotBeEmpty();

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        hasher.VerifyHashedPassword(stored, stored.PasswordHash, password)
            .Should().BeOneOf(PasswordVerificationResult.Success, PasswordVerificationResult.SuccessRehashNeeded);
    }

    [Fact]
    public async Task Register_duplicate_email_returns_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        var email = UniqueEmail();

        var first = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, username = UniqueUsername(), password = "Secret123" },
            ct);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, username = UniqueUsername(), password = "Secret123" },
            ct);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        second.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Register_duplicate_email_is_case_insensitive()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        var mixedCase = $"MixedCase-{Guid.NewGuid():N}"[..20] + "@Example.COM";

        var first = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = mixedCase, username = UniqueUsername(), password = "Secret123" },
            ct);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = mixedCase.ToLowerInvariant(), username = UniqueUsername(), password = "Secret123" },
            ct);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_duplicate_username_returns_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        var username = UniqueUsername();

        var first = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = UniqueEmail(), username, password = "Secret123" },
            ct);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = UniqueEmail(), username, password = "Secret123" },
            ct);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_duplicate_username_is_case_insensitive()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        var mixedCase = "User" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var first = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = UniqueEmail(), username = mixedCase, password = "Secret123" },
            ct);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = UniqueEmail(), username = mixedCase.ToLowerInvariant(), password = "Secret123" },
            ct);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("short1", "too short")]
    [InlineData("nodigitshere", "no digit")]
    [InlineData("12345678", "no letter")]
    public async Task Register_rejects_weak_password_with_400(string weak, string _)
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = UniqueEmail(), username = UniqueUsername(), password = weak },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Login_returns_token_and_cookie_and_persists_session()
    {
        var ct = TestContext.Current.CancellationToken;
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
        var body = await login.Content.ReadFromJsonAsync<JsonElement>(ct);
        var token = body.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(token, out _).Should().BeTrue("token should be an opaque GUID");

        login.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().ContainSingle(c => c.StartsWith("session=", StringComparison.Ordinal)
            && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sessionId = Guid.Parse(token!);
        var session = await db.Sessions.SingleAsync(s => s.Id == sessionId, ct);
        session.RevokedAt.Should().BeNull();
        session.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        var email = UniqueEmail();

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, username = UniqueUsername(), password = "Secret123" },
            ct);
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "WrongPass123" },
            ct);

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        login.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_401_to_avoid_enumeration()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = UniqueEmail(), password = "Secret123" },
            ct);

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
