using Hackaton.Api.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "/app/logs/api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

builder.Host.UseSerilog();

var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

const string WebAppCorsPolicy = "WebApp";
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebAppCorsPolicy, policy => policy
        .WithOrigins("http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    app.Logger.LogInformation("Applying EF Core migrations...");
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("EF Core migrations applied.");
}

app.UseSerilogRequestLogging();
app.UseCors(WebAppCorsPolicy);

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    var dbUp = false;
    string? dbError = null;
    try
    {
        dbUp = await db.Database.CanConnectAsync(ct);
    }
    catch (Exception ex)
    {
        dbError = ex.Message;
    }

    return Results.Ok(new HealthResponse(
        Status: dbUp ? "healthy" : "degraded",
        Database: dbUp ? "up" : "down",
        Timestamp: DateTimeOffset.UtcNow,
        Error: dbError));
});

app.Run();

public sealed record HealthResponse(string Status, string Database, DateTimeOffset Timestamp, string? Error = null);

public partial class Program;
