namespace RaceTracker.Gateway.Domain.Telemetry;

/// <summary>
/// Device run state, mirroring the firmware <c>SystemState_t</c> values carried in the
/// status payload (PROTOCOL §5). The numeric values are wire-significant.
/// </summary>
public enum DeviceState : byte
{
    Idle = 0,
    Connected = 1,
    Acquiring = 2,
}
