using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Observability;
using RaceTracker.Realtime.Application.Realtime;
using RaceTracker.Realtime.Application.Rules;
using Xunit;

namespace RaceTracker.Realtime.UnitTests;

/// <summary>
/// Unit tests for the live-relay use case (stories 6.2/6.3/8.2) with the ports mocked
/// (architecture §9): a status event is always relayed as a device-status push, a run-progress
/// push is added only while ACQUIRING a sized run, and a fired rule notifies once through the
/// TTL gate without ever disturbing the relay.
/// </summary>
public sealed class StatusRelayServiceTests : IDisposable
{
    private const string Guid = "00000000-0000-0000-0000-0000000000aa";

    private readonly IClientNotifier _notifier = Substitute.For<IClientNotifier>();
    private readonly INotificationDeduplicator _dedup = Substitute.For<INotificationDeduplicator>();
    private readonly RealtimeMetrics _metrics = new();
    private readonly StatusRelayService _service;

    public StatusRelayServiceTests()
    {
        // Default: the dedup gate lets notifications through unless a test overrides it.
        _dedup.ShouldNotifyAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _service = new StatusRelayService(
            _notifier, new RuleEngine(TimeProvider.System, NullLogger<RuleEngine>.Instance),
            _dedup, Options.Create(new RealtimeOptions()), _metrics,
            NullLogger<StatusRelayService>.Instance);
    }

    public void Dispose() => _metrics.Dispose();

    [Theory]
    [InlineData(DeviceState.Idle)]
    [InlineData(DeviceState.Connected)]
    public async Task Non_acquiring_status_pushes_only_device_status(DeviceState state)
    {
        StatusEvent statusEvent = StatusFor(state, sampledCount: 0, totalSamples: 0);

        await _service.RelayAsync(statusEvent, CancellationToken.None);

        await _notifier.Received(1).PushDeviceStatusAsync(
            Guid, Arg.Any<DeviceStatusUpdate>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().PushRunProgressAsync(
            Arg.Any<string>(), Arg.Any<RunProgressUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Acquiring_status_pushes_device_status_and_run_progress()
    {
        StatusEvent statusEvent = StatusFor(DeviceState.Acquiring, sampledCount: 416, totalSamples: 8330);

        await _service.RelayAsync(statusEvent, CancellationToken.None);

        await _notifier.Received(1).PushDeviceStatusAsync(
            Guid, Arg.Any<DeviceStatusUpdate>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).PushRunProgressAsync(
            Guid,
            Arg.Is<RunProgressUpdate>(p =>
                p.DeviceGuid == Guid && p.SampledCount == 416 && p.TotalSamples == 8330),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Acquiring_with_no_sized_run_pushes_only_device_status()
    {
        // Edge: ACQUIRING reported before the run is sized (totalSamples still 0) → no progress yet.
        StatusEvent statusEvent = StatusFor(DeviceState.Acquiring, sampledCount: 0, totalSamples: 0);

        await _service.RelayAsync(statusEvent, CancellationToken.None);

        await _notifier.Received(1).PushDeviceStatusAsync(
            Guid, Arg.Any<DeviceStatusUpdate>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().PushRunProgressAsync(
            Arg.Any<string>(), Arg.Any<RunProgressUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Device_status_push_carries_the_mapped_status_fields()
    {
        var observedAt = DateTimeOffset.UtcNow;
        var statusEvent = new StatusEvent(
            Guid, UptimeMs: 123_456, BatteryMv: 3987, BatteryPct: 76, State: DeviceState.Connected,
            SampledCount: 0, TotalSamples: 0, ErrorCode: 0x2A, ObservedAtUtc: observedAt);

        await _service.RelayAsync(statusEvent, CancellationToken.None);

        await _notifier.Received(1).PushDeviceStatusAsync(
            Guid,
            Arg.Is<DeviceStatusUpdate>(s =>
                s.DeviceGuid == Guid
                && s.State == DeviceState.Connected
                && s.UptimeMs == 123_456
                && s.BatteryMv == 3987
                && s.BatteryPct == 76
                && s.ErrorCode == 0x2A
                && s.ObservedAtUtc == observedAt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Critical_battery_status_pushes_device_status_and_counts_a_rule_event()
    {
        // 8.1: a rule-triggering status must still push live status AND surface the rule
        // event to the metric. Capture the counter via a MeterListener.
        long ruleEvents = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == RealtimeMetrics.MeterName
                && instrument.Name == "racetracker_realtime_rule_events_total")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => ruleEvents += measurement);
        listener.Start();

        var statusEvent = new StatusEvent(
            Guid, UptimeMs: 1000, BatteryMv: 2900, BatteryPct: 5, State: DeviceState.Connected,
            SampledCount: 0, TotalSamples: 0, ErrorCode: 0, ObservedAtUtc: DateTimeOffset.UnixEpoch);

        await _service.RelayAsync(statusEvent, CancellationToken.None);

        await _notifier.Received(1).PushDeviceStatusAsync(
            Guid, Arg.Any<DeviceStatusUpdate>(), Arg.Any<CancellationToken>());
        Assert.Equal(1, ruleEvents);
    }

    [Fact]
    public async Task Healthy_status_fires_no_rule_event()
    {
        long ruleEvents = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == RealtimeMetrics.MeterName
                && instrument.Name == "racetracker_realtime_rule_events_total")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => ruleEvents += measurement);
        listener.Start();

        await _service.RelayAsync(
            StatusFor(DeviceState.Connected, sampledCount: 0, totalSamples: 0),
            CancellationToken.None);

        Assert.Equal(0, ruleEvents);
    }

    [Fact]
    public async Task Critical_battery_fires_one_notification_through_the_ttl_gate()
    {
        // 8.2: the fired rule is debounced through the dedup gate, then pushed once.
        await _service.RelayAsync(CriticalBatteryStatus(), CancellationToken.None);

        await _dedup.Received(1).ShouldNotifyAsync(
            $"notify:{RuleType.BatteryCritical}:{Guid}", Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
        await _notifier.Received(1).PushNotificationAsync(
            Guid,
            Arg.Is<NotificationUpdate>(n => n.Type == RuleType.BatteryCritical && n.DeviceGuid == Guid),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Suppressed_notification_is_not_pushed_when_the_gate_is_closed()
    {
        _dedup.ShouldNotifyAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await _service.RelayAsync(CriticalBatteryStatus(), CancellationToken.None);

        await _notifier.DidNotReceive().PushNotificationAsync(
            Arg.Any<string>(), Arg.Any<NotificationUpdate>(), Arg.Any<CancellationToken>());
        // The live status push still happened.
        await _notifier.Received(1).PushDeviceStatusAsync(
            Guid, Arg.Any<DeviceStatusUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Notification_failure_never_breaks_the_live_status_relay()
    {
        // A Redis outage (dedup throws) must not stop the status push or surface as an error.
        _dedup.ShouldNotifyAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("redis down"));

        await _service.RelayAsync(CriticalBatteryStatus(), CancellationToken.None);

        await _notifier.Received(1).PushDeviceStatusAsync(
            Guid, Arg.Any<DeviceStatusUpdate>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().PushNotificationAsync(
            Arg.Any<string>(), Arg.Any<NotificationUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Healthy_status_does_not_touch_the_dedup_gate()
    {
        await _service.RelayAsync(
            StatusFor(DeviceState.Connected, sampledCount: 0, totalSamples: 0),
            CancellationToken.None);

        await _dedup.DidNotReceive().ShouldNotifyAsync(
            Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    private static StatusEvent CriticalBatteryStatus() =>
        new(Guid, UptimeMs: 1000, BatteryMv: 2900, BatteryPct: 5, State: DeviceState.Connected,
            SampledCount: 0, TotalSamples: 0, ErrorCode: 0, ObservedAtUtc: DateTimeOffset.UnixEpoch);

    private static StatusEvent StatusFor(DeviceState state, uint sampledCount, uint totalSamples) =>
        new(Guid, UptimeMs: 1000, BatteryMv: 4000, BatteryPct: 80, State: state,
            SampledCount: sampledCount, TotalSamples: totalSamples, ErrorCode: 0,
            ObservedAtUtc: DateTimeOffset.UnixEpoch);
}
