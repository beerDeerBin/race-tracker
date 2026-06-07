using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Observability;
using RaceTracker.Realtime.Application.Rules;

namespace RaceTracker.Realtime.Application.Realtime;

/// <summary>
/// The live-relay use case (stories 6.2 + 6.3): maps a normalised <see cref="StatusEvent"/> to the
/// M6 push contracts and forwards them to the device's SignalR group via the
/// <see cref="IClientNotifier"/> port. Always pushes a <see cref="DeviceStatusUpdate"/> (6.2);
/// while the device is <see cref="DeviceState.Acquiring"/> with a sized run it additionally pushes
/// a <see cref="RunProgressUpdate"/> (6.3). Since story 8.1 it runs the status through the
/// declarative <see cref="RuleEngine"/> (<c>/F70/</c>); since 8.2 each fired event is debounced
/// through the <see cref="INotificationDeduplicator"/> (TTL idempotency, <c>/F72/</c>) and, when
/// it passes the gate, pushed to the group as a notification. Rule/notify work is isolated so a
/// rule bug or a Redis outage can never break the live status relay. Pure orchestration — no
/// transport or broker dependency — so it is exercised by unit tests with mocked ports.
/// </summary>
public sealed partial class StatusRelayService
{
    private readonly IClientNotifier _notifier;
    private readonly RuleEngine _ruleEngine;
    private readonly INotificationDeduplicator _deduplicator;
    private readonly TimeSpan _notificationWindow;
    private readonly RealtimeMetrics _metrics;
    private readonly ILogger<StatusRelayService> _logger;

    public StatusRelayService(
        IClientNotifier notifier, RuleEngine ruleEngine, INotificationDeduplicator deduplicator,
        IOptions<RealtimeOptions> options, RealtimeMetrics metrics,
        ILogger<StatusRelayService> logger)
    {
        _notifier = notifier;
        _ruleEngine = ruleEngine;
        _deduplicator = deduplicator;
        // Clamp to ≥1s: a misconfigured 0/negative TTL would set a key with no expiry and
        // permanently suppress the condition.
        _notificationWindow = TimeSpan.FromSeconds(Math.Max(1, options.Value.Redis.NotificationTtlSeconds));
        _metrics = metrics;
        _logger = logger;
    }

    public async Task RelayAsync(StatusEvent statusEvent, CancellationToken cancellationToken)
    {
        string deviceGuid = statusEvent.DeviceGuid;

        var status = new DeviceStatusUpdate(
            deviceGuid, statusEvent.State, statusEvent.UptimeMs, statusEvent.BatteryMv,
            statusEvent.BatteryPct, statusEvent.ErrorCode, statusEvent.ObservedAtUtc);
        await _notifier.PushDeviceStatusAsync(deviceGuid, status, cancellationToken);
        _metrics.RecordPush("status");

        // 6.3: progress is only meaningful during a sized run; outside ACQUIRING the counters are 0.
        if (statusEvent.State == DeviceState.Acquiring && statusEvent.TotalSamples > 0)
        {
            var progress = new RunProgressUpdate(
                deviceGuid, statusEvent.SampledCount, statusEvent.TotalSamples, statusEvent.ObservedAtUtc);
            await _notifier.PushRunProgressAsync(deviceGuid, progress, cancellationToken);
            _metrics.RecordPush("progress");
        }

        // 8.1/8.2: evaluate the declarative rule table, then debounce + push each fired event.
        foreach (RuleEvent ruleEvent in _ruleEngine.Evaluate(statusEvent))
        {
            _metrics.RecordRuleEvent(ruleEvent.Type);
            LogRuleFired(ruleEvent.Type, deviceGuid, ruleEvent.Message);
            await NotifyOnceAsync(ruleEvent, cancellationToken);
        }

        LogRelayed(deviceGuid, statusEvent.State);
    }

    /// <summary>
    /// Pushes a notification for the fired event only if it passes the TTL gate (/F72/). Isolated:
    /// a Redis outage or push failure is logged and swallowed so the live status relay is never
    /// disturbed.
    /// </summary>
    private async Task NotifyOnceAsync(RuleEvent ruleEvent, CancellationToken cancellationToken)
    {
        string key = $"notify:{ruleEvent.Type}:{ruleEvent.DeviceGuid}";
        try
        {
            if (await _deduplicator.ShouldNotifyAsync(key, _notificationWindow, cancellationToken))
            {
                var notification = new NotificationUpdate(
                    ruleEvent.DeviceGuid, ruleEvent.Type, ruleEvent.Message, ruleEvent.FiredAtUtc);
                await _notifier.PushNotificationAsync(
                    ruleEvent.DeviceGuid, notification, cancellationToken);
                _metrics.RecordNotification("sent");
            }
            else
            {
                _metrics.RecordNotification("suppressed");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogNotifyFailed(ruleEvent.Type, ruleEvent.DeviceGuid, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Relayed status for device {DeviceGuid} (state {State}) to its SignalR group")]
    private partial void LogRelayed(string deviceGuid, DeviceState state);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Rule fired: {RuleType} for device {DeviceGuid} — {Message}")]
    private partial void LogRuleFired(RuleType ruleType, string deviceGuid, string message);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Notification for {RuleType} on device {DeviceGuid} failed; status relay unaffected")]
    private partial void LogNotifyFailed(RuleType ruleType, string deviceGuid, Exception exception);
}
