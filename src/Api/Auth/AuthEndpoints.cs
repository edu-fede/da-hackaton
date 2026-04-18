using Hackaton.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hackaton.Api.Auth;

public static class AuthEndpoints
{
    private const string SessionCookieName = "session";

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth");
        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        return group;
    }

    private static async Task<IResult> Register(
        [FromBody] RegisterRequest request,
        AppDbContext db,
        IPasswordHasher<User> hasher,
        CancellationToken ct)
    {
        var email = Normalize(request.Email);
        var username = Normalize(request.Username);

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing required fields",
                detail: "Email and username are required.");
        }

        if (!PasswordPolicy.TryValidate(request.Password, out var passwordError))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password",
                detail: passwordError);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = username,
            PasswordHash = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password!);

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, out var constraint))
        {
            var field = constraint.Contains("Email", StringComparison.OrdinalIgnoreCase) ? "email" : "username";
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: $"Duplicate {field}",
                detail: $"A user with this {field} already exists.");
        }

        return Results.Created(
            $"/api/users/{user.Id}",
            new UserSummary(user.Id, user.Email, user.Username, user.CreatedAt));
    }

    private static async Task<IResult> Login(
        [FromBody] LoginRequest request,
        AppDbContext db,
        IPasswordHasher<User> hasher,
        HttpContext http,
        CancellationToken ct)
    {
        var email = Normalize(request.Email);
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(request.Password))
        {
            return InvalidCredentials();
        }

        var user = await db.Users
            .Where(u => u.DeletedAt == null)
            .SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            return InvalidCredentials();
        }

        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
        }

        var now = DateTimeOffset.UtcNow;
        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt = now,
            LastSeenAt = now,
            UserAgent = Truncate(http.Request.Headers.UserAgent.ToString(), 512),
            RemoteIp = Truncate(http.Connection.RemoteIpAddress?.ToString() ?? string.Empty, 45),
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);

        http.Response.Cookies.Append(SessionCookieName, session.Id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = http.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = now.AddDays(30),
        });

        return Results.Ok(new LoginResponse(
            session.Id.ToString(),
            new UserSummary(user.Id, user.Email, user.Username, user.CreatedAt)));
    }

    private static IResult InvalidCredentials() => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Invalid credentials",
        detail: "The email or password is incorrect.");

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static bool IsUniqueViolation(DbUpdateException exception, out string constraint)
    {
        if (exception.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            constraint = pg.ConstraintName ?? string.Empty;
            return true;
        }

        constraint = string.Empty;
        return false;
    }
}
