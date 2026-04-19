using FluentAssertions;
using Hackaton.Api.Data;
using Hackaton.Api.Messages;
using Hackaton.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hackaton.Api.Tests;

public class MessageProcessorServiceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private async Task<(Guid roomId, Guid senderId)> SeedRoomAsync(CancellationToken ct)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"proc-{suffix}@example.com",
            Username = $"proc-{suffix}",
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = $"proc-{suffix}",
            Description = "processor test",
            Visibility = RoomVisibility.Public,
            OwnerId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(user);
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);
        return (room.Id, user.Id);
    }

    private async Task<List<Message>> PollRoomMessagesAsync(Guid roomId, int expectedCount, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + PollTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.Messages
                .Where(m => m.RoomId == roomId)
                .OrderBy(m => m.SequenceInRoom)
                .ToListAsync(ct);
            if (rows.Count >= expectedCount)
            {
                return rows;
            }
            await Task.Delay(PollInterval, ct);
        }

        throw new TimeoutException(
            $"Expected {expectedCount} messages for room {roomId} within {PollTimeout.TotalSeconds}s.");
    }

    private async Task<Message?> PollForMessageAsync(Guid messageId, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + PollTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Messages.SingleOrDefaultAsync(m => m.Id == messageId, ct);
            if (row is not null) return row;
            await Task.Delay(PollInterval, ct);
        }
        return null;
    }

    [Fact]
    public async Task Processes_queued_items_and_assigns_sequences()
    {
        var ct = TestContext.Current.CancellationToken;
        var (roomId, senderId) = await SeedRoomAsync(ct);
        var queue = _factory.Services.GetRequiredService<MessageQueue>();

        var items = Enumerable.Range(1, 5).Select(i => new MessageWorkItem(
            Id: Guid.NewGuid(),
            RoomId: roomId,
            SenderId: senderId,
            SenderUsername: "proc-user",
            Text: $"message {i}",
            CreatedAt: DateTimeOffset.UtcNow.AddSeconds(i),
            ReplyToMessageId: null)).ToList();

        foreach (var item in items)
        {
            await queue.Writer.WriteAsync(item, ct);
        }

        var persisted = await PollRoomMessagesAsync(roomId, 5, ct);

        persisted.Select(m => m.SequenceInRoom).Should().Equal(1, 2, 3, 4, 5);
        persisted.Select(m => m.Id).Should().BeEquivalentTo(items.Select(i => i.Id));
    }

    [Fact]
    public async Task Preserves_workitem_id_and_createdAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (roomId, senderId) = await SeedRoomAsync(ct);
        var queue = _factory.Services.GetRequiredService<MessageQueue>();

        var messageId = Guid.NewGuid();
        var sentAt = new DateTimeOffset(2026, 4, 19, 12, 34, 56, TimeSpan.Zero);
        var item = new MessageWorkItem(
            Id: messageId,
            RoomId: roomId,
            SenderId: senderId,
            SenderUsername: "proc-user",
            Text: "stamp-preserving",
            CreatedAt: sentAt,
            ReplyToMessageId: null);

        await queue.Writer.WriteAsync(item, ct);

        var row = await PollForMessageAsync(messageId, ct);
        row.Should().NotBeNull();
        row!.Id.Should().Be(messageId);
        row.CreatedAt.Should().Be(sentAt);
        row.Text.Should().Be("stamp-preserving");
        row.SequenceInRoom.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Logs_and_continues_on_failed_insert()
    {
        var ct = TestContext.Current.CancellationToken;
        var (validRoomId, senderId) = await SeedRoomAsync(ct);
        var queue = _factory.Services.GetRequiredService<MessageQueue>();

        var doomed = new MessageWorkItem(
            Id: Guid.NewGuid(),
            RoomId: Guid.NewGuid(),
            SenderId: senderId,
            SenderUsername: "proc-user",
            Text: "will fail FK",
            CreatedAt: DateTimeOffset.UtcNow,
            ReplyToMessageId: null);

        var healthy = new MessageWorkItem(
            Id: Guid.NewGuid(),
            RoomId: validRoomId,
            SenderId: senderId,
            SenderUsername: "proc-user",
            Text: "after the failure",
            CreatedAt: DateTimeOffset.UtcNow,
            ReplyToMessageId: null);

        await queue.Writer.WriteAsync(doomed, ct);
        await queue.Writer.WriteAsync(healthy, ct);

        var persisted = await PollForMessageAsync(healthy.Id, ct);
        persisted.Should().NotBeNull("the consumer must survive the earlier FK failure and keep draining");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var doomedPersisted = await db.Messages.SingleOrDefaultAsync(m => m.Id == doomed.Id, ct);
        doomedPersisted.Should().BeNull();
    }
}
