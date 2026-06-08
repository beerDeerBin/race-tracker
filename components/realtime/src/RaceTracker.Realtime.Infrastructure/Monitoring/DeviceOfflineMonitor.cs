using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Observability;
using RaceTracker.Realtime.Application.Realtime;
using RaceTracker.Realtime.Application.Rules;

namespace RaceTracker.Realtime.Infrastructure.Monitoring;

/// <summary>
/// Hosted sweep for the story-8.4 device-offline rule (<c>/F74/</c>, <c>/O70/</c>): the live relay
/// only ever sees status events, so the <b>absence</b> of one can't be detected there. This service
/// polls <see cref="DeviceActivityTracker.CollectOffline"/> every <c>OfflineSweepSeconds</c> and
/// routes each fired event through the same <see cref="RuleNotifier"/> (dedup + push) as every other
/// rule — no second notification path. A hosted <see cref="BackgroundService"/> like the relay
/// consumer; the sweep itself is a separate testable method.
/// </summary>
public sealed partial class DeviceOfflineMonitor : BackgroundService
{
    private readonly DeviceActivityTracker _tracker;
    private readonly RuleNotifier _ruleNotifier;
    private readonly RealtimeMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _sweepInterval;
    private readonly ILogger<DeviceOfflineMonitor> _logger;

    public DeviceOfflineMonitor(
        DeviceActivityTracker tracker, RuleNotifier ruleNotifier, RealtimeMetrics metrics,
        TimeProvider timeProvider, IOptions<RealtimeOptions> options,
        ILogger<DeviceOfflineMonitor> logger)
    {
        _tracker = tracker;
        _ruleNotifier = ruleNotifier;
        _metrics = metrics;
        _timeProvider = timeProvider;
        // Clamp to ≥1s so a misconfigured 0/negative interval can't spin the sweep loop.
        _sweepInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.Rules.OfflineSweepSeconds));
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_sweepInterval.TotalSeconds);
        using var timer = new PeriodicTimer(_sweepInterval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SweepAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    /// <summary>
    /// One offline sweep: notifies (deduped) for every device the tracker reports as newly offline.
    /// Separated from the timer loop so it is unit-testable without waiting on wall-clock ticks.
    /// </summary>
    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        foreach (RuleEvent offline in _tracker.CollectOffline())
        {
            _metrics.RecordRuleEvent(offline.Type);
            LogOffline(offline.DeviceGuid, offline.Message);
            await _ruleNotifier.NotifyAsync(offline, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Device-offline monitor started; sweeping every {IntervalSeconds}s")]
    private partial void LogStarted(double intervalSeconds);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Device offline: {DeviceGuid} — {Message}")]
    private partial void LogOffline(string deviceGuid, string message);
}
