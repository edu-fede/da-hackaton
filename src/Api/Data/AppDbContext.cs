using Microsoft.EntityFrameworkCore;

namespace Hackaton.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();

    public DbSet<RoomBan> RoomBans => Set<RoomBan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(user =>
        {
            user.HasKey(u => u.Id);
            user.Property(u => u.Email).IsRequired().HasMaxLength(320);
            user.Property(u => u.Username).IsRequired().HasMaxLength(32);
            user.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
            user.Property(u => u.CreatedAt).IsRequired();
            user.HasIndex(u => u.Email).IsUnique();
            user.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<Session>(session =>
        {
            session.HasKey(s => s.Id);
            session.Property(s => s.UserAgent).IsRequired().HasMaxLength(512);
            session.Property(s => s.RemoteIp).IsRequired().HasMaxLength(45);
            session.Property(s => s.CreatedAt).IsRequired();
            session.Property(s => s.LastSeenAt).IsRequired();
            session.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            session.HasIndex(s => s.UserId);
        });

        modelBuilder.Entity<Room>(room =>
        {
            room.HasKey(r => r.Id);
            room.Property(r => r.Name).IsRequired().HasMaxLength(64);
            room.Property(r => r.Description).IsRequired().HasMaxLength(1024);
            room.Property(r => r.Visibility).IsRequired();
            room.Property(r => r.CreatedAt).IsRequired();
            room.HasOne(r => r.Owner)
                .WithMany()
                .HasForeignKey(r => r.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
            room.HasIndex(r => r.Name)
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL");
            room.HasIndex(r => r.OwnerId);
        });

        modelBuilder.Entity<RoomMember>(member =>
        {
            member.HasKey(m => new { m.RoomId, m.UserId });
            member.Property(m => m.Role).IsRequired();
            member.Property(m => m.JoinedAt).IsRequired();
            member.HasOne(m => m.Room)
                .WithMany()
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            member.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            member.HasIndex(m => m.UserId);
        });

        modelBuilder.Entity<RoomBan>(ban =>
        {
            ban.HasKey(b => new { b.RoomId, b.UserId });
            ban.Property(b => b.BannedAt).IsRequired();
            ban.Property(b => b.Reason).HasMaxLength(512);
            ban.HasOne(b => b.Room)
                .WithMany()
                .HasForeignKey(b => b.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            ban.HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            ban.HasOne(b => b.BannedBy)
                .WithMany()
                .HasForeignKey(b => b.BannedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            ban.HasIndex(b => b.UserId);
        });
    }
}
