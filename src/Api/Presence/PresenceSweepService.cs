using Hackaton.Api.Messages;
using Microsoft.AspNetCore.SignalR;

namespace Hackaton.Api.Presence;

/// <summary>
/// Demotes users to <see cref="PresenceStatus.AFK"/> when all their connections' heartbeats
/// have exceeded the threshold (CLAUDE.md §2 Architecture Constraint: inactivity is inferred
/// server-side — browsers hibernate inactive tabs and cannot self-report). Fan-out happens
/// only on transitions, so steady-state traffic is zero.
/// </summary>
public sealed class PresenceSweepService(
    PresenceTracker tracker,
    IHubContext<ChatHub> hubContext,
    ILogger<PresenceSweepService> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AfkThreshold = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "PresenceSweepService started; sweep every {IntervalSec}s, AFK after {ThresholdSec}s.",
            SweepInterval.TotalSeconds,
            AfkThreshold.TotalSeconds);

        using var timer = new PeriodicTimer(SweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var transitions = tracker.SweepAfk(DateTimeOffset.UtcNow, AfkThreshold);
                foreach (var transition in transitions)
                {
                    await PresenceBroadcaster.FanOutAsync(hubContext, transition, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("PresenceSweepService stopping; cancellation requested.");
        }
    }
}
