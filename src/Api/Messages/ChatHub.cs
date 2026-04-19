using System.Security.Claims;
using System.Text;
using Hackaton.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Hackaton.Api.Messages;

/// <summary>
/// Real-time chat hub (CLAUDE.md §1 fast path). Messages are written to an in-process
/// <see cref="MessageQueue"/> and broadcast to the SignalR group — no DB access on the
/// <see cref="SendMessage"/> hot path. The BackgroundService consumer in Story 1.12
/// persists items asynchronously.
/// </summary>
[Authorize]
public class ChatHub(AppDbContext db, MessageQueue queue) : Hub
{
    private const string JoinedRoomsKey = "JoinedRooms";
    private const int MaxTextBytes = 3 * 1024;

    public override Task OnConnectedAsync()
    {
        Context.Items[JoinedRoomsKey] = new HashSet<Guid>();
        return base.OnConnectedAsync();
    }

    public async Task JoinRoom(Guid roomId)
    {
        var userId = CurrentUserId();
        var isMember = await db.RoomMembers
            .AnyAsync(m => m.RoomId == roomId && m.UserId == userId);
        if (!isMember)
        {
            throw new HubException("You are not a member of this room.");
        }

        var banned = await db.RoomBans
            .AnyAsync(b => b.RoomId == roomId && b.UserId == userId);
        if (banned)
        {
            throw new HubException("You are banned from this room.");
        }

        JoinedRooms().Add(roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
    }

    public async Task LeaveRoom(Guid roomId)
    {
        JoinedRooms().Remove(roomId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(roomId));
    }

    public async Task<MessageBroadcast> SendMessage(Guid roomId, string text, Guid? replyToMessageId)
    {
        if (!JoinedRooms().Contains(roomId))
        {
            throw new HubException("Join the room before sending messages.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new HubException("Message text is required.");
        }

        if (Encoding.UTF8.GetByteCount(text) > MaxTextBytes)
        {
            throw new HubException("Message exceeds the 3 KB limit.");
        }

        var item = new MessageWorkItem(
            Id: Guid.NewGuid(),
            RoomId: roomId,
            SenderId: CurrentUserId(),
            SenderUsername: CurrentUsername(),
            Text: text,
            CreatedAt: DateTimeOffset.UtcNow,
            ReplyToMessageId: replyToMessageId);

        await queue.Writer.WriteAsync(item, Context.ConnectionAborted);

        var broadcast = new MessageBroadcast(
            Id: item.Id,
            RoomId: item.RoomId,
            SenderId: item.SenderId,
            SenderUsername: item.SenderUsername,
            Text: item.Text,
            CreatedAt: item.CreatedAt,
            ReplyToMessageId: item.ReplyToMessageId,
            SequenceInRoom: null);

        await Clients.Group(RoomGroup(roomId)).SendAsync("MessageReceived", broadcast);

        return broadcast;
    }

    private HashSet<Guid> JoinedRooms()
    {
        if (Context.Items.TryGetValue(JoinedRoomsKey, out var value) && value is HashSet<Guid> rooms)
        {
            return rooms;
        }

        var fresh = new HashSet<Guid>();
        Context.Items[JoinedRoomsKey] = fresh;
        return fresh;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string CurrentUsername() =>
        Context.User!.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    private static string RoomGroup(Guid roomId) => $"room:{roomId}";
}
