using FluentAssertions;
using Hackaton.Api.Data;
using Hackaton.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Hackaton.Api.Tests;

public class RoomsPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture = fixture;

    private static User NewUser(string suffix) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"rooms-{suffix}@example.com",
        Username = $"rooms-{suffix}",
        PasswordHash = "hash",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private async Task<User> SeedUserAsync(CancellationToken ct)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = NewUser(suffix);
        await using var context = _fixture.CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(ct);
        return user;
    }

    [Fact]
    public async Task Room_round_trips_through_postgres()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await SeedUserAsync(ct);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var roomId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero);

        await using (var writer = _fixture.CreateContext())
        {
            writer.Rooms.Add(new Room
            {
                Id = roomId,
                Name = $"room-{suffix}",
                Description = "test room description",
                Visibility = RoomVisibility.Public,
                OwnerId = owner.Id,
                CreatedAt = createdAt,
            });
            await writer.SaveChangesAsync(ct);
        }

        await using var reader = _fixture.CreateContext();
        var persisted = await reader.Rooms.SingleAsync(r => r.Id == roomId, ct);

        persisted.Name.Should().Be($"room-{suffix}");
        persisted.Description.Should().Be("test room description");
        persisted.Visibility.Should().Be(RoomVisibility.Public);
        persisted.OwnerId.Should().Be(owner.Id);
        persisted.CreatedAt.Should().Be(createdAt);
        persisted.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task RoomMember_round_trips_with_composite_key()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await SeedUserAsync(ct);
        var member = await SeedUserAsync(ct);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var roomId = Guid.NewGuid();
        var joinedAt = new DateTimeOffset(2026, 4, 18, 12, 15, 0, TimeSpan.Zero);

        await using (var writer = _fixture.CreateContext())
        {
            writer.Rooms.Add(new Room
            {
                Id = roomId,
                Name = $"room-member-{suffix}",
                Description = "room with member",
                Visibility = RoomVisibility.Private,
                OwnerId = owner.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            writer.RoomMembers.Add(new RoomMember
            {
                RoomId = roomId,
                UserId = member.Id,
                Role = RoomRole.Admin,
                JoinedAt = joinedAt,
            });
            await writer.SaveChangesAsync(ct);
        }

        await using var reader = _fixture.CreateContext();
        var persisted = await reader.RoomMembers
            .SingleAsync(m => m.RoomId == roomId && m.UserId == member.Id, ct);

        persisted.Role.Should().Be(RoomRole.Admin);
        persisted.JoinedAt.Should().Be(joinedAt);
    }

    [Fact]
    public async Task RoomBan_round_trips_through_postgres()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await SeedUserAsync(ct);
        var banned = await SeedUserAsync(ct);
        var admin = await SeedUserAsync(ct);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var roomId = Guid.NewGuid();
        var bannedAt = new DateTimeOffset(2026, 4, 18, 12, 30, 0, TimeSpan.Zero);

        await using (var writer = _fixture.CreateContext())
        {
            writer.Rooms.Add(new Room
            {
                Id = roomId,
                Name = $"room-ban-{suffix}",
                Description = "room with ban",
                Visibility = RoomVisibility.Public,
                OwnerId = owner.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            writer.RoomBans.Add(new RoomBan
            {
                RoomId = roomId,
                UserId = banned.Id,
                BannedByUserId = admin.Id,
                BannedAt = bannedAt,
                Reason = "spamming",
            });
            await writer.SaveChangesAsync(ct);
        }

        await using var reader = _fixture.CreateContext();
        var persisted = await reader.RoomBans
            .SingleAsync(b => b.RoomId == roomId && b.UserId == banned.Id, ct);

        persisted.BannedByUserId.Should().Be(admin.Id);
        persisted.BannedAt.Should().Be(bannedAt);
        persisted.Reason.Should().Be("spamming");
    }

    [Fact]
    public async Task Duplicate_room_name_is_rejected_among_active_rooms()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await SeedUserAsync(ct);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var name = $"dup-room-{suffix}";

        await using var context = _fixture.CreateContext();
        context.Rooms.Add(new Room
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "first",
            Visibility = RoomVisibility.Public,
            OwnerId = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        context.Rooms.Add(new Room
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "second",
            Visibility = RoomVisibility.Public,
            OwnerId = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var act = async () => await context.SaveChangesAsync(ct);

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Room_name_can_be_reused_after_soft_delete()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await SeedUserAsync(ct);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var name = $"reuse-{suffix}";
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        await using (var writer = _fixture.CreateContext())
        {
            writer.Rooms.Add(new Room
            {
                Id = firstId,
                Name = name,
                Description = "first incarnation",
                Visibility = RoomVisibility.Public,
                OwnerId = owner.Id,
                CreatedAt = DateTimeOffset.UtcNow,
                DeletedAt = DateTimeOffset.UtcNow,
            });
            await writer.SaveChangesAsync(ct);
        }

        await using (var writer = _fixture.CreateContext())
        {
            writer.Rooms.Add(new Room
            {
                Id = secondId,
                Name = name,
                Description = "second incarnation",
                Visibility = RoomVisibility.Public,
                OwnerId = owner.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            var act = async () => await writer.SaveChangesAsync(ct);
            await act.Should().NotThrowAsync();
        }

        await using var reader = _fixture.CreateContext();
        var active = await reader.Rooms.SingleAsync(r => r.Id == secondId, ct);
        active.DeletedAt.Should().BeNull();
    }
}
