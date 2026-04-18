namespace Hackaton.Api.Auth;

public sealed record RegisterRequest(string? Email, string? Username, string? Password);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record UserSummary(Guid Id, string Email, string Username, DateTimeOffset CreatedAt);

public sealed record LoginResponse(string Token, UserSummary User);
