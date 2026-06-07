using Microsoft.Extensions.Logging;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Observability;
using RaceTracker.Realtime.Application.Rules;

namespace RaceTracker.Realtime.Application.Realtime;

/// <summary>
/// The live-relay use case (stories 6.2 + 6.3): maps a normalised <see cref="StatusEvent"/> to the
/// M6 push contracts and forwards them to the device's SignalR group via the
/// <see cref="IClientNotifier"/> port. Always pushes a <see cref="DeviceStatusUpdate"/> (6.2);
/// while the device is <see cref="DeviceState.Acquiring"/> with a sized run it additionally pushes
/// a <see cref="RunProgressUpdate"/> (6.3). Since story 8.1 it also runs the status through the
/// declarative <see cref="RuleEngine"/> (<c>/F70/</c>), surfacing fired events to log + metric
/// (dedup/persist/dispatch come in 8.2/8.3). Pure orchestration — no transport or broker
/// dependency — so it is exercised by unit tests with a mocked notifier.
/// </summary>
public sealed partial class StatusRelayService
{
    private readonly IClientNotifier _notifier;
    private readonly RuleEngine _ruleEngine;
    private readonly RealtimeMetrics _metrics;
    private readonly ILogger<StatusRelayService> _logger;

    public StatusRelayService(
        IClientNotifier notifier, RuleEngine ruleEngine, RealtimeMetrics metrics,
        ILogger<StatusRelayService> logger)
    {
        _notifier = notifier;
        _ruleEngine = ruleEngine;
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

        // 8.1: evaluate the declarative rule table; for now fired events go to log + metric only.
        foreach (RuleEvent ruleEvent in _ruleEngine.Evaluate(statusEvent))
        {
            _metrics.RecordRuleEvent(ruleEvent.Type);
            LogRuleFired(ruleEvent.Type, deviceGuid, ruleEvent.Message);
        }

        LogRelayed(deviceGuid, statusEvent.State);
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Relayed status for device {DeviceGuid} (state {State}) to its SignalR group")]
    private partial void LogRelayed(string deviceGuid, DeviceState state);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Rule fired: {RuleType} for device {DeviceGuid} — {Message}")]
    private partial void LogRuleFired(RuleType ruleType, string deviceGuid, string message);
}
