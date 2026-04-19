namespace Hackaton.Api.Data;

/// <summary>
/// A chat message persisted under a specific room with a monotonically increasing
/// per-room <see cref="SequenceInRoom"/> (unique per <see cref="RoomId"/>).
/// </summary>
/// <remarks>
/// The 3 KB per-message text cap from the task (§2.5.2) is enforced at the service
/// layer when messages are accepted via SignalR (Story 1.11+), not at the column level,
/// because the cap is a byte limit and Postgres VARCHAR(n) enforces characters.
/// The <see cref="SenderId"/> FK uses <c>OnDelete(Restrict)</c> on purpose: Story 2.10
/// (account deletion) must make a deliberate choice between nulling sender, soft-deleting
/// the user, or using a tombstone row — cascading here would silently erase group history.
/// </remarks>
public class Message
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public Room? Room { get; set; }

    public Guid SenderId { get; set; }

    public User? Sender { get; set; }

    public required string Text { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? EditedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? ReplyToMessageId { get; set; }

    public Message? ReplyToMessage { get; set; }

    public int SequenceInRoom { get; set; }
}
