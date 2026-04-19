namespace Hackaton.Api.Data;

/// <summary>Chat room. Names are unique among non-deleted rooms (see CLAUDE.md §3 — names can be reused after a room is deleted; ids are never reused).</summary>
public class Room
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public RoomVisibility Visibility { get; set; }

    public Guid OwnerId { get; set; }

    public User? Owner { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
