using Microsoft.EntityFrameworkCore;

namespace Hackaton.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
