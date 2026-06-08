using Microsoft.Extensions.Logging.Abstractions;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Realtime.Application.Rules;
using Shouldly;
using Xunit;

namespace RaceTracker.Realtime.UnitTests;

/// <summary>
/// Unit tests for the declarative rule engine (stories 8.1/8.4, <c>/F70/</c>/<c>/F71/</c>/<c>/F74/</c>):
/// the battery-critical rule fires on a low reading or the error bit, never on the unknown sentinel;
/// the story-8.4 error-code rule fires on any non-zero mask and coexists with the battery rule; and
/// an all-healthy status yields no events. Pure logic — no infrastructure.
/// </summary>
public sealed class RuleEngineTests
{
    private const string Guid = "00000000-0000-0000-0000-0000000000aa";
    private static readonly DateTimeOffset _fixedNow = new(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly RuleEngine _engine =
        new(new FixedTimeProvider(_fixedNow), NullLogger<RuleEngine>.Instance);

    /// <summary>Minimal fixed-clock TimeProvider (avoids a test-only package dependency).</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    [Fact]
    public void Battery_critical_fires_on_low_voltage()
    {
        IReadOnlyList<RuleEvent> events = _engine.Evaluate(Status(batteryMv: 3000, errorCode: 0));

        RuleEvent fired = events.ShouldHaveSingleItem();
        fired.Type.ShouldBe(RuleType.BatteryCritical);
        fired.DeviceGuid.ShouldBe(Guid);
        fired.FiredAtUtc.ShouldBe(_fixedNow);
        fired.Message.ShouldContain(Guid);
    }

    [Fact]
    public void Battery_critical_fires_on_the_error_bit_even_with_a_healthy_voltage()
    {
        // Bit 42 = PWR_BATTERY_CRITICAL_ERROR. (Since 8.4 the generic error-code rule also fires on
        // any non-zero mask, so assert the battery rule is present rather than that it's alone.)
        ulong errorCode = 1UL << 42;

        IReadOnlyList<RuleEvent> events = _engine.Evaluate(Status(batteryMv: 4000, errorCode));

        events.ShouldContain(e => e.Type == RuleType.BatteryCritical);
    }

    [Fact]
    public void Battery_critical_does_not_fire_on_the_unknown_sentinel()
    {
        // 65535 mV = "unknown" — must never read as critical.
        IReadOnlyList<RuleEvent> events = _engine.Evaluate(Status(batteryMv: 65535, errorCode: 0));

        events.ShouldBeEmpty();
    }

    [Fact]
    public void Battery_critical_fires_on_the_error_bit_even_with_unknown_voltage()
    {
        // Unknown ADC reading but the firmware asserted the critical flag → still fires.
        IReadOnlyList<RuleEvent> events = _engine.Evaluate(Status(batteryMv: 65535, 1UL << 42));

        events.ShouldContain(e => e.Type == RuleType.BatteryCritical);
    }

    [Fact]
    public void Battery_critical_does_not_fire_on_an_unrelated_error_bit()
    {
        // Bit 25 = MQTT_PUBLISH_ERROR — proves the battery mask is bit-specific, not "any error"
        // (the generic error-code rule does fire on it, but the battery rule must not).
        IReadOnlyList<RuleEvent> events = _engine.Evaluate(Status(batteryMv: 4000, 1UL << 25));

        events.ShouldNotContain(e => e.Type == RuleType.BatteryCritical);
    }

    [Fact]
    public void Battery_critical_fires_just_below_the_threshold()
    {
        IReadOnlyList<RuleEvent> events =
            _engine.Evaluate(Status(batteryMv: RuleSet.BatteryCriticalMv - 1, errorCode: 0));

        events.ShouldHaveSingleItem().Type.ShouldBe(RuleType.BatteryCritical);
    }

    [Fact]
    public void No_rule_fires_for_a_healthy_status()
    {
        IReadOnlyList<RuleEvent> events = _engine.Evaluate(Status(batteryMv: 4000, errorCode: 0));

        events.ShouldBeEmpty();
    }

    [Fact]
    public void Error_code_rule_fires_on_any_non_zero_mask()
    {
        // 8.4 (/F74/): a non-battery error bit (bit 25 = MQTT_PUBLISH_ERROR) trips the error-code rule.
        IReadOnlyList<RuleEvent> events = _engine.Evaluate(Status(batteryMv: 4000, 1UL << 25));

        RuleEvent fired = events.ShouldHaveSingleItem();
        fired.Type.ShouldBe(RuleType.ErrorCode);
        fired.Message.ShouldContain(Guid);
    }

    [Fact]
    public void Error_code_rule_does_not_fire_on_a_zero_mask()
    {
        IReadOnlyList<RuleEvent> events = _engine.Evaluate(Status(batteryMv: 4000, errorCode: 0));

        events.ShouldNotContain(e => e.Type == RuleType.ErrorCode);
    }

    [Fact]
    public void Battery_critical_bit_fires_both_battery_and_error_code_rules()
    {
        // 8.4: a critical-battery mask trips both rules (distinct dedup keys downstream) — the
        // error-code rule fires on any non-zero mask, including the battery bit.
        IReadOnlyList<RuleEvent> events = _engine.Evaluate(Status(batteryMv: 4000, 1UL << 42));

        events.Select(e => e.Type).ShouldBe(
            [RuleType.BatteryCritical, RuleType.ErrorCode], ignoreOrder: true);
    }

    [Fact]
    public void Just_above_the_threshold_does_not_fire()
    {
        IReadOnlyList<RuleEvent> events =
            _engine.Evaluate(Status(batteryMv: RuleSet.BatteryCriticalMv, errorCode: 0));

        events.ShouldBeEmpty();
    }

    private static StatusEvent Status(ushort batteryMv, ulong errorCode) =>
        new(Guid, UptimeMs: 1000, BatteryMv: batteryMv, BatteryPct: 50, State: DeviceState.Connected,
            SampledCount: 0, TotalSamples: 0, ErrorCode: errorCode,
            ObservedAtUtc: DateTimeOffset.UnixEpoch);
}
