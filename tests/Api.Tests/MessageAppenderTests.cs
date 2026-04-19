using FluentAssertions;
using Hackaton.Api.Data;
using Hackaton.Api.Messages;
using Hackaton.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hackaton.Api.Tests;

public class MessageAppenderTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture = fixture;

    private async Task<(Guid roomId, Guid senderId)> SeedRoomAsync(CancellationToken ct)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"appender-{suffix}@example.com",
            Username = $"app-{suffix}",
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = $"room-{suffix}",
            Description = "append test",
            Visibility = RoomVisibility.Public,
            OwnerId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await using var ctx = _fixture.CreateContext();
        ctx.Users.Add(user);
        ctx.Rooms.Add(room);
        await ctx.SaveChangesAsync(ct);
        return (room.Id, user.Id);
    }

    [Fact]
    public async Task Append_assigns_sequential_sequence_per_room()
    {
        var ct = TestContext.Current.CancellationToken;
        var (roomId, senderId) = await SeedRoomAsync(ct);

        await using var ctx = _fixture.CreateContext();
        var first = await MessageAppender.AppendAsync(ctx, roomId, senderId, "one", ct: ct);
        var second = await MessageAppender.AppendAsync(ctx, roomId, senderId, "two", ct: ct);
        var third = await MessageAppender.AppendAsync(ctx, roomId, senderId, "three", ct: ct);

        first.SequenceInRoom.Should().Be(1);
        second.SequenceInRoom.Should().Be(2);
        third.SequenceInRoom.Should().Be(3);
    }

    [Fact]
    public async Task Append_sequence_is_scoped_per_room()
    {
        var ct = TestContext.Current.CancellationToken;
        var (roomA, senderA) = await SeedRoomAsync(ct);
        var (roomB, senderB) = await SeedRoomAsync(ct);

        await using var ctx = _fixture.CreateContext();
        var a1 = await MessageAppender.AppendAsync(ctx, roomA, senderA, "a1", ct: ct);
        var b1 = await MessageAppender.AppendAsync(ctx, roomB, senderB, "b1", ct: ct);
        var a2 = await MessageAppender.AppendAsync(ctx, roomA, senderA, "a2", ct: ct);
        var b2 = await MessageAppender.AppendAsync(ctx, roomB, senderB, "b2", ct: ct);

        a1.SequenceInRoom.Should().Be(1);
        a2.SequenceInRoom.Should().Be(2);
        b1.SequenceInRoom.Should().Be(1);
        b2.SequenceInRoom.Should().Be(2);
    }

    [Fact]
    public async Task Append_under_concurrent_writes_produces_no_gaps_no_duplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        var (roomId, senderId) = await SeedRoomAsync(ct);
        const int concurrent = 20;

        var tasks = Enumerable.Range(0, concurrent)
            .Select(i => Task.Run(async () =>
            {
                await using var ctx = _fixture.CreateContext();
                return await MessageAppender.AppendAsync(ctx, roomId, senderId, $"msg-{i}", ct: ct);
            }, ct))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(concurrent);
        var sequences = results.Select(m => m.SequenceInRoom).OrderBy(x => x).ToList();
        sequences.Should().Equal(Enumerable.Range(1, concurrent));

        await using var verify = _fixture.CreateContext();
        var persisted = await verify.Messages
            .Where(m => m.RoomId == roomId)
            .Select(m => m.SequenceInRoom)
            .OrderBy(x => x)
            .ToListAsync(ct);
        persisted.Should().Equal(Enumerable.Range(1, concurrent));
    }
}
