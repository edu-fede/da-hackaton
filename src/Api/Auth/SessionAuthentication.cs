using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Hackaton.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hackaton.Api.Auth;

public static class SessionAuthenticationDefaults
{
    public const string SchemeName = "Session";

    public const string CookieName = "session";

    public const string SessionIdClaimType = "sid";
}

/// <summary>
/// Authenticates requests by looking up the <c>session</c> cookie value in the <c>Sessions</c> table.
/// Rejects sessions whose row is missing, revoked, or whose user has been soft-deleted.
/// </summary>
public sealed class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AppDbContext _db;

    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(SessionAuthenticationDefaults.CookieName, out var token) ||
            string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        if (!Guid.TryParse(token, out var sessionId))
        {
            return AuthenticateResult.Fail("Malformed session token.");
        }

        var session = await _db.Sessions
            .Include(s => s.User)
            .SingleOrDefaultAsync(s =>
                s.Id == sessionId &&
                s.RevokedAt == null &&
                s.User!.DeletedAt == null);

        if (session is null)
        {
            return AuthenticateResult.Fail("Session is invalid, revoked, or belongs to a deleted user.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new Claim(ClaimTypes.Name, session.User!.Username),
            new Claim(ClaimTypes.Email, session.User.Email),
            new Claim(SessionAuthenticationDefaults.SessionIdClaimType, session.Id.ToString()),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            title = "Unauthorized",
            status = 401,
        });
        await Response.Body.WriteAsync(payload);
    }
}
