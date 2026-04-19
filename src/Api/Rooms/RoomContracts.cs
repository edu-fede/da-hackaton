using Hackaton.Api.Data;

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
