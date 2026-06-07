using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Observability;
using RaceTracker.Realtime.Application.Realtime;
using RaceTracker.Realtime.Application.Rules;
using Xunit;

namespace RaceTracker.Realtime.UnitTests;

/// <summary>
/// Unit tests for the live-relay use case (stories 6.2/6.3) with the <see cref="IClientNotifier"/>
/// port mocked (architecture §9): a status event is always relayed as a device-status push, and a
/// run-progress push is added only while ACQUIRING a sized run.
/// </summary>
public sealed class StatusRelayServiceTests : IDisposable
{
    private const string Guid = "00000000-0000-0000-0000-0000000000aa";

    private readonly IClientNotifier _notifier = Substitute.For<IClientNotifier>();
    private readonly RealtimeMetrics _metrics = new();
    private readonly StatusRelayService _service;

    public StatusRelayServiceTests() =>
        _service = new StatusRelayService(
            _notifier, new RuleEngine(TimeProvider.System, NullLogger<RuleEngine>.Instance),
            _metrics, NullLogger<StatusRelayService>.Instance);

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

    private static StatusEvent StatusFor(DeviceState state, uint sampledCount, uint totalSamples) =>
        new(Guid, UptimeMs: 1000, BatteryMv: 4000, BatteryPct: 80, State: state,
            SampledCount: sampledCount, TotalSamples: totalSamples, ErrorCode: 0,
            ObservedAtUtc: DateTimeOffset.UnixEpoch);
}
