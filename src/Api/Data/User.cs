namespace Hackaton.Api.Data;

/// <summary>Application user. Identified by a <see cref="Guid"/>; email and username are unique.</summary>
public class User
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string Username { get; set; }

    public required string PasswordHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
