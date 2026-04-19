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
