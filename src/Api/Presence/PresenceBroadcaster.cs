using Hackaton.Api.Messages;
using Microsoft.AspNetCore.SignalR;

namespace Hackaton.Api.Presence;

public sealed record PresenceBroadcast(Guid UserId, string Status, DateTimeOffset At);

internal static class PresenceBroadcaster
{
    public const string EventName = "PresenceChanged";

    public static async Task FanOutAsync(
        IHubContext<ChatHub> hubContext,
        PresenceTransition transition,
        CancellationToken ct = default)
    {
        var payload = new PresenceBroadcast(
            transition.UserId,
            transition.NewStatus.ToString(),
            transition.At);

        foreach (var roomId in transition.Rooms)
        {
            await hubContext.Clients
                .Group(ChatHub.RoomGroup(roomId))
                .SendAsync(EventName, payload, ct);
        }
    }

    public static async Task FanOutAsync(
        IHubCallerClients clients,
        PresenceTransition transition)
    {
        var payload = new PresenceBroadcast(
            transition.UserId,
            transition.NewStatus.ToString(),
            transition.At);

        foreach (var roomId in transition.Rooms)
        {
            await clients.Group(ChatHub.RoomGroup(roomId)).SendAsync(EventName, payload);
        }
    }
}
