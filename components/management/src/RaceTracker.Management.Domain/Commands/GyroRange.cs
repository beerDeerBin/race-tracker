namespace RaceTracker.Management.Domain.Commands;

/// <summary>
/// Gyroscope full-scale range for a run (PROTOCOL.md §4.2 <c>gyroRange</c>). The underlying value is
/// the exact wire byte (deliberately <b>non-contiguous</b> — the LSM6DSOX register codes); the member
/// name is the physical range in ±dps. <c>0x02</c> (±500 dps) is the device default.
/// </summary>
public enum GyroRange : byte
{
    Dps250 = 0x00,
    Dps125 = 0x01,
    Dps500 = 0x02,
    Dps1000 = 0x04,
    Dps2000 = 0x06,
}
