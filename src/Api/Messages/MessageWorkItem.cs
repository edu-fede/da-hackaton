namespace Hackaton.Api.Messages;

/// <summary>Work item queued by the SignalR Hub for the BackgroundService consumer (Story 1.12) to persist.</summary>
public sealed record MessageWorkItem(
    Guid Id,
    Guid RoomId,
    Guid SenderId,
    string SenderUsername,
    string Text,
    DateTimeOffset CreatedAt,
    Guid? ReplyToMessageId);
