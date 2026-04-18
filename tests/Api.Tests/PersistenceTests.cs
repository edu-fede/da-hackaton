using FluentAssertions;
using Hackaton.Api.Data;
using Hackaton.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Hackaton.Api.Tests;

public class PersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture = fixture;

    [Fact]
    public async Task User_round_trips_through_postgres()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var createdAt = new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero);

        await using (var writer = _fixture.CreateContext())
        {
            writer.Users.Add(new User
            {
                Id = userId,
                Email = $"alice-{suffix}@example.com",
                Username = $"alice-{suffix}",
                PasswordHash = "hash-placeholder",
                CreatedAt = createdAt,
            });
            await writer.SaveChangesAsync(ct);
        }

        await using var reader = _fixture.CreateContext();
        var persisted = await reader.Users.SingleAsync(u => u.Id == userId, ct);

        persisted.Email.Should().Be($"alice-{suffix}@example.com");
        persisted.Username.Should().Be($"alice-{suffix}");
        persisted.PasswordHash.Should().Be("hash-placeholder");
        persisted.CreatedAt.Should().Be(createdAt);
        persisted.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_by_unique_index()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"dup-email-{suffix}@example.com";

        await using var context = _fixture.CreateContext();
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = $"user-a-{suffix}",
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = $"user-b-{suffix}",
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var act = async () => await context.SaveChangesAsync(ct);

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Duplicate_username_is_rejected_by_unique_index()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"dup-name-{suffix}";

        await using var context = _fixture.CreateContext();
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = $"a-{suffix}@example.com",
            Username = username,
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = $"b-{suffix}@example.com",
            Username = username,
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var act = async () => await context.SaveChangesAsync(ct);

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }
}
