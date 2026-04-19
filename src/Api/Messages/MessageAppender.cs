using Hackaton.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hackaton.Api.Messages;

/// <summary>
/// Appends a message to a room, assigning <see cref="Message.SequenceInRoom"/> with a
/// retry-on-unique-violation loop (CLAUDE.md §3 option b). Safe under concurrent writes
/// at the target 300-user scale; for extreme contention bump <see cref="MaxAttempts"/>.
/// </summary>
public static class MessageAppender
{
    private const int MaxAttempts = 10;

    public static async Task<Message> AppendAsync(
        AppDbContext db,
        Guid roomId,
        Guid senderId,
        string text,
        Guid? replyToMessageId = null,
        CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var nextSequence = (await db.Messages
                .Where(m => m.RoomId == roomId)
                .MaxAsync(m => (int?)m.SequenceInRoom, ct)) ?? 0;

            var message = new Message
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                SenderId = senderId,
                Text = text,
                CreatedAt = DateTimeOffset.UtcNow,
                ReplyToMessageId = replyToMessageId,
                SequenceInRoom = nextSequence + 1,
            };
            db.Messages.Add(message);

            try
            {
                await db.SaveChangesAsync(ct);
                return message;
            }
            catch (DbUpdateException ex) when (IsSequenceUniqueViolation(ex))
            {
                db.Entry(message).State = EntityState.Detached;
                // Widen backoff with attempt number to spread contending tasks apart.
                var delayMs = Random.Shared.Next(5, 20) * (attempt + 1);
                await Task.Delay(delayMs, ct);
            }
        }

        throw new InvalidOperationException(
            $"Failed to assign SequenceInRoom for room {roomId} after {MaxAttempts} attempts.");
    }

    private static bool IsSequenceUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException pg &&
        pg.SqlState == PostgresErrorCodes.UniqueViolation &&
        (pg.ConstraintName?.Contains("SequenceInRoom", StringComparison.OrdinalIgnoreCase) ?? false);
}
