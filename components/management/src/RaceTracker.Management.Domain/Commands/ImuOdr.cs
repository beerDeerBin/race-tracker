using System.Diagnostics.CodeAnalysis;

namespace RaceTracker.Management.Domain.Commands;

/// <summary>
/// IMU output data rate for a run (PROTOCOL.md §4.2 <c>odr</c>). The underlying value is the exact
/// wire byte; the member name is the physical rate in Hz. Also fixes the time base of the stored
/// samples (<c>t = index / odr_hz</c>, <c>/F54/</c>). <c>0x04</c> (104 Hz) is the device default.
/// </summary>
public enum ImuOdr : byte
{
    /// <summary>12.5 Hz — the underscore stands in for the decimal point in the physical rate.</summary>
    [SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
        Justification = "The underscore encodes the decimal point of the 12.5 Hz physical rate.")]
    Hz12_5 = 0x01,
    Hz26 = 0x02,
    Hz52 = 0x03,
    Hz104 = 0x04,
    Hz208 = 0x05,
    Hz417 = 0x06,
    Hz833 = 0x07,
}
