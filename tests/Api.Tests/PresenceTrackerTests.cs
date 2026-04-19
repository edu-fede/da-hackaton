using FluentAssertions;
using Hackaton.Api.Presence;
using Xunit;

namespace Hackaton.Api.Tests;

public class PresenceTrackerTests
{
    private readonly PresenceTracker _tracker = new();
    private readonly DateTimeOffset _t0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _room = Guid.NewGuid();
    private static readonly TimeSpan AfkThreshold = TimeSpan.FromSeconds(60);

    [Fact]
    public void TrackConnection_first_connection_returns_Online_transition()
    {
        var transition = _tracker.TrackConnection(_user, "c1", [_room], _t0);

        transition.Should().NotBeNull();
        transition!.UserId.Should().Be(_user);
        transition.NewStatus.Should().Be(PresenceStatus.Online);
        transition.Rooms.Should().Contain(_room);
        transition.At.Should().Be(_t0);
    }

    [Fact]
    public void TrackConnection_second_connection_for_same_user_returns_no_transition()
    {
        _tracker.TrackConnection(_user, "c1", [_room], _t0);

        var transition = _tracker.TrackConnection(_user, "c2", [_room], _t0);

        transition.Should().BeNull();
    }

    [Fact]
    public void TrackConnection_merges_rooms_across_connections()
    {
        var room2 = Guid.NewGuid();
        _tracker.TrackConnection(_user, "c1", [_room], _t0);
        _tracker.TrackConnection(_user, "c2", [room2], _t0);

        var transition = _tracker.RemoveConnection(_user, "c1", _t0);

        transition.Should().BeNull();
        var offline = _tracker.RemoveConnection(_user, "c2", _t0);
        offline!.Rooms.Should().BeEquivalentTo(new[] { _room, room2 });
    }

    [Fact]
    public void RemoveConnection_not_last_returns_no_transition()
    {
        _tracker.TrackConnection(_user, "c1", [_room], _t0);
        _tracker.TrackConnection(_user, "c2", [_room], _t0);

        var transition = _tracker.RemoveConnection(_user, "c1", _t0);

        transition.Should().BeNull();
    }

    [Fact]
    public void RemoveConnection_last_returns_Offline_transition()
    {
        _tracker.TrackConnection(_user, "c1", [_room], _t0);

        var transition = _tracker.RemoveConnection(_user, "c1", _t0.AddSeconds(5));

        transition.Should().NotBeNull();
        transition!.NewStatus.Should().Be(PresenceStatus.Offline);
        transition.Rooms.Should().Contain(_room);
        transition.At.Should().Be(_t0.AddSeconds(5));
    }

    [Fact]
    public void RemoveConnection_for_unknown_user_returns_null()
    {
        var transition = _tracker.RemoveConnection(_user, "c1", _t0);

        transition.Should().BeNull();
    }

    [Fact]
    public void Heartbeat_on_online_user_returns_no_transition()
    {
        _tracker.TrackConnection(_user, "c1", [_room], _t0);

        var transition = _tracker.Heartbeat(_user, "c1", _t0.AddSeconds(15));

        transition.Should().BeNull();
    }

    [Fact]
    public void Heartbeat_on_afk_user_returns_Online_transition()
    {
        _tracker.TrackConnection(_user, "c1", [_room], _t0);
        _tracker.SweepAfk(_t0.AddSeconds(61), AfkThreshold);

        var transition = _tracker.Heartbeat(_user, "c1", _t0.AddSeconds(62));

        transition.Should().NotBeNull();
        transition!.NewStatus.Should().Be(PresenceStatus.Online);
        transition.Rooms.Should().Contain(_room);
    }

    [Fact]
    public void Heartbeat_for_unknown_user_returns_null()
    {
        var transition = _tracker.Heartbeat(_user, "c1", _t0);

        transition.Should().BeNull();
    }

    [Fact]
    public void Heartbeat_for_unknown_connection_of_known_user_returns_null()
    {
        _tracker.TrackConnection(_user, "c1", [_room], _t0);

        var transition = _tracker.Heartbeat(_user, "c-unknown", _t0);

        transition.Should().BeNull();
    }

    [Fact]
    public void SweepAfk_when_all_connections_stale_returns_AFK_transition()
    {
        _tracker.TrackConnection(_user, "c1", [_room], _t0);

        var transitions = _tracker.SweepAfk(_t0.AddSeconds(61), AfkThreshold);

        transitions.Should().ContainSingle();
        transitions[0].UserId.Should().Be(_user);
        transitions[0].NewStatus.Should().Be(PresenceStatus.AFK);
        transitions[0].Rooms.Should().Contain(_room);
    }

    [Fact]
    public void SweepAfk_when_one_connection_still_fresh_returns_no_transition()
    {
        _tracker.TrackConnection(_user, "c1", [_room], _t0);
        _tracker.TrackConnection(_user, "c2", [_room], _t0.AddSeconds(30));

        var transitions = _tracker.SweepAfk(_t0.AddSeconds(61), AfkThreshold);

        transitions.Should().NotContain(t => t.UserId == _user);
    }

    [Fact]
    public void SweepAfk_for_already_afk_user_does_not_repeat_transition()
    {
        _tracker.TrackConnection(_user, "c1", [_room], _t0);
        _tracker.SweepAfk(_t0.AddSeconds(61), AfkThreshold);

        var transitions = _tracker.SweepAfk(_t0.AddSeconds(120), AfkThreshold);

        transitions.Should().BeEmpty();
    }

    [Fact]
    public void AddRoom_updates_rooms_used_in_subsequent_transitions()
    {
        _tracker.TrackConnection(_user, "c1", [], _t0);
        var newRoom = Guid.NewGuid();

        _tracker.AddRoom(_user, newRoom);
        var offline = _tracker.RemoveConnection(_user, "c1", _t0);

        offline!.Rooms.Should().Contain(newRoom);
    }

    [Fact]
    public void RemoveRoom_updates_rooms_used_in_subsequent_transitions()
    {
        _tracker.TrackConnection(_user, "c1", [_room], _t0);

        _tracker.RemoveRoom(_user, _room);
        var offline = _tracker.RemoveConnection(_user, "c1", _t0);

        offline!.Rooms.Should().NotContain(_room);
    }

    [Fact]
    public void AddRoom_for_unknown_user_is_noop()
    {
        var act = () => _tracker.AddRoom(_user, _room);
        act.Should().NotThrow();
    }

    [Fact]
    public void RemoveRoom_for_unknown_user_is_noop()
    {
        var act = () => _tracker.RemoveRoom(_user, _room);
        act.Should().NotThrow();
    }

    [Fact]
    public void TrackConnection_after_user_went_offline_returns_Online_transition_again()
    {
        _tracker.TrackConnection(_user, "c1", [_room], _t0);
        _tracker.RemoveConnection(_user, "c1", _t0);

        var transition = _tracker.TrackConnection(_user, "c2", [_room], _t0.AddSeconds(10));

        transition.Should().NotBeNull();
        transition!.NewStatus.Should().Be(PresenceStatus.Online);
    }
}
