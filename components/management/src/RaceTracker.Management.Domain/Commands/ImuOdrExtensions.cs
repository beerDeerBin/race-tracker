namespace RaceTracker.Management.Domain.Commands;

/// <summary>
/// Maps an <see cref="ImuOdr"/> wire value to its physical rate in Hz — the run's time base
/// (<c>t = index / odr_hz</c>, <c>/F54/</c>). Management is the only service that knows a run's ODR
/// (the device never echoes it back), so it resolves the rate here before announcing it on the
/// run-metadata event.
/// </summary>
public static class ImuOdrExtensions
{
    /// <summary>
    /// The physical sample rate in Hz. Note: 12.5 Hz is rounded to <c>13</c> — the persisted
    /// <c>odr_hz</c> column is an integer, so the one fractional rate cannot be represented exactly.
    /// Every other rate (26/52/104/208/417/833) is exact.
    /// </summary>
    public static int ToHz(this ImuOdr odr) => odr switch
    {
        ImuOdr.Hz12_5 => 13,
        ImuOdr.Hz26 => 26,
        ImuOdr.Hz52 => 52,
        ImuOdr.Hz104 => 104,
        ImuOdr.Hz208 => 208,
        ImuOdr.Hz417 => 417,
        ImuOdr.Hz833 => 833,
        _ => 104,
    };
}
