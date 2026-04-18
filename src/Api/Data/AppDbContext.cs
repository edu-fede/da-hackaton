using Microsoft.EntityFrameworkCore;

namespace Hackaton.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Session> Sessions => Set<Session>();

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
    }
}
