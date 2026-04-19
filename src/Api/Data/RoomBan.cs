namespace Hackaton.Api.Data;

/// <summary>Ban of a user from a room. <see cref="BannedByUserId"/> is nullable so bans survive the banner's account deletion with audit set to null.</summary>
public class RoomBan
{
    public Guid RoomId { get; set; }

    public Room? Room { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public Guid? BannedByUserId { get; set; }

    public User? BannedBy { get; set; }

    public DateTimeOffset BannedAt { get; set; }

    public string? Reason { get; set; }
}
