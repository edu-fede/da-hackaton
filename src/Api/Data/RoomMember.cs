namespace Hackaton.Api.Data;

/// <summary>Membership of a user in a room, with their role. Composite key (RoomId, UserId).</summary>
public class RoomMember
{
    public Guid RoomId { get; set; }

    public Room? Room { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public RoomRole Role { get; set; }

    public DateTimeOffset JoinedAt { get; set; }
}
