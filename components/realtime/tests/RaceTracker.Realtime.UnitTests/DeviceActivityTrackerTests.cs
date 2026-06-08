using Microsoft.Extensions.Options;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Realtime;
using RaceTracker.Realtime.Application.Rules;
using Shouldly;
using Xunit;

namespace RaceTracker.Realtime.UnitTests;

/// <summary>
/// Unit tests for the stateful story-8.4 detectors (<c>/F74/</c>, <c>/O70/</c>): <c>Observe</c>
/// raises a run-finished event only on the ACQUIRING→idle/connected transition (never on a cold
/// start), and <c>CollectOffline</c> reports a silent device once per offline episode, re-arming
/// after a fresh observation. Pure logic over a controllable clock — no infrastructure.
/// </summary>
public sealed class DeviceActivityTrackerTests
{
    private const string Guid = "00000000-0000-0000-0000-0000000000aa";
    private static readonly DateTimeOffset _start = new(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly MutableTimeProvider _clock = new(_start);
    private readonly DeviceActivityTracker _tracker;

    public DeviceActivityTrackerTests()
    {
        // 15 s offline threshold (the /O70/ default).
        IOptions<RealtimeOptions> options = Options.Create(new RealtimeOptions
        {
            Rules = new RulesOptions { OfflineThresholdSeconds = 15 },
        });
        _tracker = new DeviceActivityTracker(_clock, options);
    }

    [Theory]
    [InlineData(DeviceState.Connected)]
    [InlineData(DeviceState.Idle)]
    public void Observe_fires_run_finished_when_leaving_acquiring(DeviceState endState)
    {
        _tracker.Observe(Status(DeviceState.Acquiring)).ShouldBeNull();

        RuleEvent? finished = _tracker.Observe(Status(endState));

        finished.ShouldNotBeNull();
        finished.Type.ShouldBe(RuleType.RunFinished);
        finished.DeviceGuid.ShouldBe(Guid);
        finished.FiredAtUtc.ShouldBe(_start);
    }

    [Fact]
    public void Observe_does_not_fire_run_finished_on_a_cold_start()
    {
        // The very first status for a device only seeds state — no prior, no transition.
        _tracker.Observe(Status(DeviceState.Connected)).ShouldBeNull();
    }

    [Theory]
    [InlineData(DeviceState.Idle, DeviceState.Connected)]
    [InlineData(DeviceState.Connected, DeviceState.Acquiring)]
    [InlineData(DeviceState.Idle, DeviceState.Acquiring)]
    public void Observe_does_not_fire_on_a_non_finishing_transition(DeviceState from, DeviceState to)
    {
        _tracker.Observe(Status(from));

        _tracker.Observe(Status(to)).ShouldBeNull();
    }

    [Fact]
    public void Offline_is_reported_for_a_device_past_the_threshold()
    {
        _tracker.Observe(Status(DeviceState.Connected));

        _clock.Advance(TimeSpan.FromSeconds(16));
        IReadOnlyList<RuleEvent> offline = _tracker.CollectOffline();

        RuleEvent fired = offline.ShouldHaveSingleItem();
        fired.Type.ShouldBe(RuleType.DeviceOffline);
        fired.DeviceGuid.ShouldBe(Guid);
    }

    [Fact]
    public void A_recently_seen_device_is_not_offline()
    {
        _tracker.Observe(Status(DeviceState.Connected));

        _clock.Advance(TimeSpan.FromSeconds(15)); // exactly the threshold is still alive (strict >)
        _tracker.CollectOffline().ShouldBeEmpty();
    }

    [Fact]
    public void Offline_is_reported_only_once_per_episode()
    {
        _tracker.Observe(Status(DeviceState.Connected));
        _clock.Advance(TimeSpan.FromSeconds(16));

        _tracker.CollectOffline().ShouldHaveSingleItem();

        // Still silent on the next sweep — already flagged, don't re-alert.
        _clock.Advance(TimeSpan.FromSeconds(16));
        _tracker.CollectOffline().ShouldBeEmpty();
    }

    [Fact]
    public void A_fresh_observation_re_arms_the_offline_alert()
    {
        _tracker.Observe(Status(DeviceState.Connected));
        _clock.Advance(TimeSpan.FromSeconds(16));
        _tracker.CollectOffline().ShouldHaveSingleItem();

        // Device comes back, then goes silent again → it must alert a second time.
        _tracker.Observe(Status(DeviceState.Connected));
        _clock.Advance(TimeSpan.FromSeconds(16));

        _tracker.CollectOffline().ShouldHaveSingleItem().Type.ShouldBe(RuleType.DeviceOffline);
    }

    private static StatusEvent Status(DeviceState state) =>
        new(Guid, UptimeMs: 1000, BatteryMv: 4000, BatteryPct: 80, State: state,
            SampledCount: 0, TotalSamples: 0, ErrorCode: 0, ObservedAtUtc: DateTimeOffset.UnixEpoch);
}
