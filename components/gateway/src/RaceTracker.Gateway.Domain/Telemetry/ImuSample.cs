namespace RaceTracker.Gateway.Domain.Telemetry;

/// <summary>
/// A single decoded IMU reading — the typed form of the packed 24-byte
/// <c>SampleRecord_t</c> (PROTOCOL §6): three acceleration axes (m/s²) and three angular
/// rates (rad/s). No timestamp; the time base is derived from the run's ODR.
/// </summary>
public sealed record ImuSample(
    float Ax,
    float Ay,
    float Az,
    float Gx,
    float Gy,
    float Gz);
