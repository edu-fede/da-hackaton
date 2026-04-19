using Hackaton.Api.Data;
using Hackaton.Api.Presence;

namespace Hackaton.Api.Rooms;

public sealed record CreateRoomRequest(string? Name, string? Description, RoomVisibility Visibility);

public sealed record RoomSummary(
    Guid Id,
    string Name,
    string Description,
    RoomVisibility Visibility,
    int MemberCount,
    DateTimeOffset CreatedAt);

public sealed record RoomCatalogEntry(
    Guid Id,
    string Name,
    string Description,
    int MemberCount);

public sealed record MyRoomEntry(
    Guid Id,
    string Name,
    string Description,
    RoomVisibility Visibility,
    RoomRole Role,
    int MemberCount);

public sealed record RoomMemberEntry(
    Guid UserId,
    string Username,
    RoomRole Role,
    PresenceStatus Status);
