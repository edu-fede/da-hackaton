using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hackaton.Api.Data;

/// <summary>Used by `dotnet ef` at design time. Connection string is a placeholder — the tool only reads the model.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=hackaton;Username=hackaton;Password=hackaton")
            .Options;
        return new AppDbContext(options);
    }
}
