namespace RaceTracker.Realtime.Application.Rules;

/// <summary>
/// The declarative rule table (<c>/F70/</c>): rules are <b>data</b>, not code. Adding a rule is
/// a new entry here — the <see cref="RuleEngine"/> loop is generic and never changes. Story 8.1
/// seeds the battery-critical rule (<c>/F71/</c>); 8.4 appends run-finished / offline /
/// error-code rows.
/// </summary>
public static class RuleSet
{
    /// <summary>Battery voltage in mV below which the battery counts as critical (/F71/).</summary>
    public const ushort BatteryCriticalMv = 3100;

    /// <summary>Sentinel battery value meaning "unknown" (PROTOCOL §5) — never treated as critical.</summary>
    private const ushort BatteryUnknownMv = 65535;

    /// <summary>Bit 42 of the error-code bitmask: <c>PWR_BATTERY_CRITICAL_ERROR</c> (PROTOCOL §5.1).</summary>
    private const ulong BatteryCriticalErrorBit = 1UL << 42;

    public static readonly IReadOnlyList<RuleDefinition> Rules =
    [
        new RuleDefinition(
            RuleType.BatteryCritical,
            static status =>
                (status.BatteryMv != BatteryUnknownMv && status.BatteryMv < BatteryCriticalMv)
                || (status.ErrorCode & BatteryCriticalErrorBit) != 0,
            static status =>
                $"Battery critical on device {status.DeviceGuid} "
                + $"({status.BatteryMv} mV, errorCode 0x{status.ErrorCode:X})"),
    ];
}
