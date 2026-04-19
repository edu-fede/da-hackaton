using System.Security.Claims;
using System.Text.Json.Serialization;
using Hackaton.Api.Auth;
using Hackaton.Api.Data;
using Hackaton.Api.Rooms;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
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

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    options.UseNpgsql(config.GetConnectionString("Default"));
});

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services
    .AddAuthentication(SessionAuthenticationDefaults.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
        SessionAuthenticationDefaults.SchemeName,
        _ => { });
builder.Services.AddAuthorization();

const string WebAppCorsPolicy = "WebApp";
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebAppCorsPolicy, policy => policy
        .WithOrigins("http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
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
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapRoomEndpoints();

app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new
{
    id = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
    email = user.FindFirstValue(ClaimTypes.Email),
    username = user.FindFirstValue(ClaimTypes.Name),
})).RequireAuthorization();

app.MapGet("/api/me/rooms", async (
    ClaimsPrincipal principal,
    AppDbContext db,
    CancellationToken ct) =>
{
    var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var rooms = await db.RoomMembers
        .Where(m => m.UserId == userId && m.Room!.DeletedAt == null)
        .OrderBy(m => m.Room!.Name)
        .Select(m => new MyRoomEntry(
            m.Room!.Id,
            m.Room.Name,
            m.Room.Description,
            m.Room.Visibility,
            m.Role,
            db.RoomMembers.Count(x => x.RoomId == m.RoomId)))
        .ToListAsync(ct);
    return Results.Ok(rooms);
}).RequireAuthorization();

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
