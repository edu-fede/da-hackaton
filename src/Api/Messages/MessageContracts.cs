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
