namespace RaceTracker.Management.Domain.Commands;

/// <summary>
/// Accelerometer full-scale range for a run (PROTOCOL.md §4.2 <c>accelRange</c>). The underlying
/// value is the exact wire byte (deliberately <b>non-contiguous</b> — the LSM6DSOX register codes);
/// the member name is the physical range in ±g. <c>0x02</c> (±4 g) is the device default.
/// </summary>
public enum AccelRange : byte
{
    G2 = 0x00,
    G16 = 0x01,
    G4 = 0x02,
    G8 = 0x03,
}
