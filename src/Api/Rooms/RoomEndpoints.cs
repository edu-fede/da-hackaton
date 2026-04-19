using System.Security.Claims;
using Hackaton.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hackaton.Api.Rooms;

public static class RoomEndpoints
{
    public static RouteGroupBuilder MapRoomEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/rooms").RequireAuthorization();
        group.MapPost("/", CreateRoom);
        group.MapGet("/", ListRooms);
        group.MapPost("/{id:guid}/join", JoinRoom);
        group.MapPost("/{id:guid}/leave", LeaveRoom);
        group.MapGet("/{id:guid}/members", GetMembers);
        return group;
    }

    private static async Task<IResult> CreateRoom(
        [FromBody] CreateRoomRequest request,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Name is required");
        }

        if (request.Visibility != RoomVisibility.Public && request.Visibility != RoomVisibility.Private)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Visibility must be Public or Private");
        }

        var description = (request.Description ?? string.Empty).Trim();
        var roomId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        db.Rooms.Add(new Room
        {
            Id = roomId,
            Name = name,
            Description = description,
            Visibility = request.Visibility,
            OwnerId = userId,
            CreatedAt = now,
        });

        db.RoomMembers.Add(new RoomMember
        {
            RoomId = roomId,
            UserId = userId,
            Role = RoomRole.Owner,
            JoinedAt = now,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Duplicate room name",
                detail: "A room with this name already exists.");
        }

        return Results.Created(
            $"/api/rooms/{roomId}",
            new RoomSummary(roomId, name, description, request.Visibility, MemberCount: 1, now));
    }

    private static async Task<IResult> ListRooms(
        AppDbContext db,
        [FromQuery] string? q,
        CancellationToken ct)
    {
        var query = db.Rooms
            .Where(r => r.Visibility == RoomVisibility.Public && r.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(r =>
                EF.Functions.ILike(r.Name, pattern) ||
                EF.Functions.ILike(r.Description, pattern));
        }

        var rooms = await query
            .OrderBy(r => r.Name)
            .Select(r => new RoomCatalogEntry(
                r.Id,
                r.Name,
                r.Description,
                db.RoomMembers.Count(m => m.RoomId == r.Id)))
            .ToListAsync(ct);

        return Results.Ok(rooms);
    }

    private static async Task<IResult> JoinRoom(
        Guid id,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var room = await db.Rooms
            .SingleOrDefaultAsync(r => r.Id == id && r.DeletedAt == null, ct);
        if (room is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Room not found");
        }

        // Idempotent: if the caller is already a member, short-circuit BEFORE the
        // visibility/ban gates. This lets the frontend unconditionally call /join
        // before opening a Hub connection without worrying about the user's prior
        // membership state (Your Rooms vs. catalog navigation, direct URL, etc.).
        var already = await db.RoomMembers
            .AnyAsync(m => m.RoomId == id && m.UserId == userId, ct);
        if (already)
        {
            return Results.NoContent();
        }

        if (room.Visibility != RoomVisibility.Public)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Room is not publicly joinable",
                detail: room.Visibility == RoomVisibility.Private
                    ? "Private rooms require an invitation."
                    : "Personal chats cannot be joined via this endpoint.");
        }

        var banned = await db.RoomBans
            .AnyAsync(b => b.RoomId == id && b.UserId == userId, ct);
        if (banned)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "You are banned from this room");
        }

        db.RoomMembers.Add(new RoomMember
        {
            RoomId = id,
            UserId = userId,
            Role = RoomRole.Member,
            JoinedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> LeaveRoom(
        Guid id,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var membership = await db.RoomMembers
            .SingleOrDefaultAsync(m => m.RoomId == id && m.UserId == userId, ct);
        if (membership is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not a member of this room");
        }

        if (membership.Role == RoomRole.Owner)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Owner cannot leave the room",
                detail: "Delete the room instead.");
        }

        db.RoomMembers.Remove(membership);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> GetMembers(
        Guid id,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var roomExists = await db.Rooms
            .AnyAsync(r => r.Id == id && r.DeletedAt == null, ct);
        if (!roomExists)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Room not found");
        }

        var isMember = await db.RoomMembers
            .AnyAsync(m => m.RoomId == id && m.UserId == userId, ct);
        if (!isMember)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "You are not a member of this room");
        }

        // Full roster — acceptable for MVP. For rooms approaching NFR-2's 1000-member cap
        // pagination would be required (cursor on (Role, Username) with a server page size).
        var members = await db.RoomMembers
            .Where(m => m.RoomId == id)
            .Select(m => new RoomMemberEntry(
                m.UserId,
                m.User!.Username,
                m.Role))
            .ToListAsync(ct);

        return Results.Ok(members);
    }
}
