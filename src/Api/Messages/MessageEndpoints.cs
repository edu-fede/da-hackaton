using System.Security.Claims;
using Hackaton.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hackaton.Api.Messages;

public static class MessageEndpoints
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    private const int MaxRoomsPerResync = 100;
    private const int MaxMessagesPerResyncRoom = 500;

    public static RouteGroupBuilder MapMessageEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/rooms/{id:guid}/messages").RequireAuthorization();
        group.MapGet("/", GetMessages);
        return group;
    }

    public static IEndpointRouteBuilder MapResyncEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/rooms/resync", Resync).RequireAuthorization();
        return routes;
    }

    private static async Task<IResult> Resync(
        [FromBody] WatermarkEntry[] watermarks,
        AppDbContext db,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        if (watermarks.Length > MaxRoomsPerResync)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Too many rooms in a single resync call",
                detail: $"At most {MaxRoomsPerResync} rooms per request; received {watermarks.Length}.");
        }

        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var results = new List<ResyncRoomResult>(watermarks.Length);

        foreach (var entry in watermarks)
        {
            var lastSeq = Math.Max(0, entry.LastSeq);

            var isMember = await db.RoomMembers
                .AnyAsync(m =>
                    m.RoomId == entry.RoomId &&
                    m.UserId == userId &&
                    m.Room!.DeletedAt == null,
                    ct);
            if (!isMember)
            {
                results.Add(new ResyncRoomResult(entry.RoomId, NotAMember: true, Messages: null));
                continue;
            }

            var tail = await db.Messages
                .Where(m => m.RoomId == entry.RoomId && m.SequenceInRoom > lastSeq)
                .OrderBy(m => m.SequenceInRoom)
                .Take(MaxMessagesPerResyncRoom)
                .Select(m => new MessageEntry(
                    m.Id,
                    m.RoomId,
                    m.SenderId,
                    m.Sender!.Username,
                    m.DeletedAt == null ? m.Text : null,
                    m.CreatedAt,
                    m.EditedAt,
                    m.DeletedAt,
                    m.ReplyToMessageId,
                    m.SequenceInRoom))
                .ToListAsync(ct);

            results.Add(new ResyncRoomResult(entry.RoomId, NotAMember: false, Messages: tail));
        }

        return Results.Ok(results);
    }

    private static async Task<IResult> GetMessages(
        Guid id,
        AppDbContext db,
        ClaimsPrincipal principal,
        [FromQuery] int? beforeSeq,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

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

        var query = db.Messages.Where(m => m.RoomId == id);
        if (beforeSeq is int cursor)
        {
            query = query.Where(m => m.SequenceInRoom < cursor);
        }

        var page = await query
            .OrderByDescending(m => m.SequenceInRoom)
            .Take(take)
            .Select(m => new MessageEntry(
                m.Id,
                m.RoomId,
                m.SenderId,
                m.Sender!.Username,
                m.DeletedAt == null ? m.Text : null,
                m.CreatedAt,
                m.EditedAt,
                m.DeletedAt,
                m.ReplyToMessageId,
                m.SequenceInRoom))
            .ToListAsync(ct);

        return Results.Ok(page);
    }
}
