namespace Hackaton.Api.Messages;

public sealed record MessageEntry(
    Guid Id,
    Guid RoomId,
    Guid SenderId,
    string SenderUsername,
    string? Text,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    DateTimeOffset? DeletedAt,
    Guid? ReplyToMessageId,
    int SequenceInRoom);

/// <summary>
/// Shape returned to the sender (ack) and broadcast to the room group via the SignalR Hub.
/// <see cref="SequenceInRoom"/> is <c>null</c> during Story 1.11 — the BackgroundService
/// consumer in Story 1.12 assigns it when persisting and a later broadcast (or the history
/// resync path, Story 1.13) will carry the real value.
/// </summary>
public sealed record MessageBroadcast(
    Guid Id,
    Guid RoomId,
    Guid SenderId,
    string SenderUsername,
    string Text,
    DateTimeOffset CreatedAt,
    Guid? ReplyToMessageId,
    int? SequenceInRoom);

public sealed record EditMessageRequest(string? Text);

/// <summary>Broadcast when a message is edited. Carries the new text and the EditedAt timestamp.</summary>
public sealed record MessageEditedBroadcast(
    Guid Id,
    Guid RoomId,
    string Text,
    DateTimeOffset EditedAt);

/// <summary>Broadcast when a message is soft-deleted. Text is omitted — clients render a placeholder.</summary>
public sealed record MessageDeletedBroadcast(
    Guid Id,
    Guid RoomId,
    DateTimeOffset DeletedAt);

/// <summary>Client-supplied watermark: "for this room, I have seen up to sequence <see cref="LastSeq"/>".</summary>
public sealed record WatermarkEntry(Guid RoomId, int LastSeq);

/// <summary>
/// One entry in the resync response, corresponding to one input <see cref="WatermarkEntry"/>.
/// If <see cref="NotAMember"/> is true, the caller should discard its local watermark for that room.
/// Otherwise <see cref="Messages"/> carries the missing tail (sequence &gt; LastSeq) in ascending order,
/// capped server-side.
/// </summary>
public sealed record ResyncRoomResult(
    Guid RoomId,
    bool NotAMember,
    IReadOnlyList<MessageEntry>? Messages);
