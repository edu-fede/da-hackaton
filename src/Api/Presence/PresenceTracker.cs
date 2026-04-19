using System.Collections.Concurrent;

namespace Hackaton.Api.Presence;

public enum PresenceStatus
{
    Online,
    AFK,
    Offline,
}

public sealed record PresenceTransition(
    Guid UserId,
    PresenceStatus NewStatus,
    IReadOnlyList<Guid> Rooms,
    DateTimeOffset At);

internal sealed class PresenceInfo
{
    public readonly ConcurrentDictionary<string, DateTimeOffset> Connections = new();
    public readonly HashSet<Guid> Rooms = new();
    public PresenceStatus Status = PresenceStatus.Online;
    public readonly object Sync = new();
}

/// <summary>
/// In-memory presence state (CLAUDE.md §2 Architecture Constraint). One singleton instance per
/// API process. Never persists to the DB. Connections and lastHeartbeat timestamps live here;
/// status transitions produced by these methods drive SignalR fan-out in <c>ChatHub</c> and
/// <c>PresenceSweepService</c>.
/// </summary>
public sealed class PresenceTracker
{
    private readonly ConcurrentDictionary<Guid, PresenceInfo> _users = new();

    public PresenceTransition? TrackConnection(
        Guid userId,
        string connectionId,
        IEnumerable<Guid> rooms,
        DateTimeOffset now)
    {
        var info = _users.GetOrAdd(userId, _ => new PresenceInfo());

        lock (info.Sync)
        {
            var wasOnlineWithConnections = info.Connections.Count > 0 && info.Status == PresenceStatus.Online;

            info.Connections[connectionId] = now;
            foreach (var roomId in rooms)
            {
                info.Rooms.Add(roomId);
            }
            info.Status = PresenceStatus.Online;

            return wasOnlineWithConnections
                ? null
                : new PresenceTransition(userId, PresenceStatus.Online, info.Rooms.ToArray(), now);
        }
    }

    public PresenceTransition? RemoveConnection(Guid userId, string connectionId, DateTimeOffset now)
    {
        if (!_users.TryGetValue(userId, out var info))
        {
            return null;
        }

        lock (info.Sync)
        {
            info.Connections.TryRemove(connectionId, out _);
            if (!info.Connections.IsEmpty)
            {
                return null;
            }

            var rooms = info.Rooms.ToArray();
            _users.TryRemove(userId, out _);
            return new PresenceTransition(userId, PresenceStatus.Offline, rooms, now);
        }
    }

    public PresenceTransition? Heartbeat(Guid userId, string connectionId, DateTimeOffset now)
    {
        if (!_users.TryGetValue(userId, out var info))
        {
            return null;
        }

        lock (info.Sync)
        {
            if (!info.Connections.ContainsKey(connectionId))
            {
                return null;
            }

            info.Connections[connectionId] = now;

            if (info.Status != PresenceStatus.AFK)
            {
                return null;
            }

            info.Status = PresenceStatus.Online;
            return new PresenceTransition(userId, PresenceStatus.Online, info.Rooms.ToArray(), now);
        }
    }

    public IReadOnlyList<PresenceTransition> SweepAfk(DateTimeOffset now, TimeSpan afkThreshold)
    {
        var transitions = new List<PresenceTransition>();

        foreach (var (userId, info) in _users)
        {
            lock (info.Sync)
            {
                if (info.Status != PresenceStatus.Online || info.Connections.IsEmpty)
                {
                    continue;
                }

                var allStale = info.Connections.Values.All(lastHeartbeat => now - lastHeartbeat > afkThreshold);
                if (!allStale)
                {
                    continue;
                }

                info.Status = PresenceStatus.AFK;
                transitions.Add(new PresenceTransition(userId, PresenceStatus.AFK, info.Rooms.ToArray(), now));
            }
        }

        return transitions;
    }

    public void AddRoom(Guid userId, Guid roomId)
    {
        if (!_users.TryGetValue(userId, out var info))
        {
            return;
        }

        lock (info.Sync)
        {
            info.Rooms.Add(roomId);
        }
    }

    public void RemoveRoom(Guid userId, Guid roomId)
    {
        if (!_users.TryGetValue(userId, out var info))
        {
            return;
        }

        lock (info.Sync)
        {
            info.Rooms.Remove(roomId);
        }
    }
}
