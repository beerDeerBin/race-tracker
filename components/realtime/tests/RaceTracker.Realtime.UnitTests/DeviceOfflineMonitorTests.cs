using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Observability;
using RaceTracker.Realtime.Application.Realtime;
using RaceTracker.Realtime.Application.Rules;
using RaceTracker.Realtime.Infrastructure.Monitoring;
using Xunit;

namespace RaceTracker.Realtime.UnitTests;

/// <summary>
/// Unit tests for the hosted offline sweep (story 8.4): one sweep notifies, through the shared
/// <see cref="RuleNotifier"/> path, for each device the tracker reports as newly offline — and a
/// sweep with nothing stale notifies nothing. Since story 8.3 the notifier enqueues to the outbox
/// (the dispatcher pushes), so the sweep is asserted via the outbox. Exercises the testable sweep
/// method directly (no timer loop); the tracker + clock supply the offline condition.
/// </summary>
public sealed class DeviceOfflineMonitorTests : IDisposable
{
    private const string Guid = "00000000-0000-0000-0000-0000000000aa";
    private static readonly DateTimeOffset _start = new(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly INotificationDeduplicator _dedup = Substitute.For<INotificationDeduplicator>();
    private readonly INotificationOutbox _outbox = Substitute.For<INotificationOutbox>();
    private readonly RealtimeMetrics _metrics = new();
    private readonly MutableTimeProvider _clock = new(_start);
    private readonly DeviceActivityTracker _tracker;
    private readonly DeviceOfflineMonitor _monitor;

    public DeviceOfflineMonitorTests()
    {
        _dedup.ShouldNotifyAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        IOptions<RealtimeOptions> options = Options.Create(new RealtimeOptions
        {
            Rules = new RulesOptions { OfflineThresholdSeconds = 15, OfflineSweepSeconds = 5 },
        });
        _tracker = new DeviceActivityTracker(_clock, options);
        var ruleNotifier = new RuleNotifier(
            _dedup, _outbox, options, _metrics, NullLogger<RuleNotifier>.Instance);
        _monitor = new DeviceOfflineMonitor(
            _tracker, ruleNotifier, _metrics, _clock, options,
            NullLogger<DeviceOfflineMonitor>.Instance);
    }

    public void Dispose() => _metrics.Dispose();

    [Fact]
    public async Task A_sweep_notifies_for_a_device_that_went_offline()
    {
        _tracker.Observe(Status());
        _clock.Advance(TimeSpan.FromSeconds(16));

        await _monitor.SweepAsync(CancellationToken.None);

        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<RuleEvent>(e => e.Type == RuleType.DeviceOffline && e.DeviceGuid == Guid),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_sweep_with_nothing_stale_notifies_nothing()
    {
        _tracker.Observe(Status()); // just seen — not offline yet

        await _monitor.SweepAsync(CancellationToken.None);

        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Any<RuleEvent>(), Arg.Any<CancellationToken>());
    }

    private static StatusEvent Status() =>
        new(Guid, UptimeMs: 1000, BatteryMv: 4000, BatteryPct: 80, State: DeviceState.Connected,
            SampledCount: 0, TotalSamples: 0, ErrorCode: 0, ObservedAtUtc: DateTimeOffset.UnixEpoch);
}
