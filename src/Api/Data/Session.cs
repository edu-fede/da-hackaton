namespace Hackaton.Api.Data;

/// <summary>Authenticated session. The <see cref="Id"/> doubles as the opaque session token.</summary>
public class Session
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public required string UserAgent { get; set; }

    public required string RemoteIp { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
