namespace RaceTracker.Persistence.Domain.Telemetry;

/// <summary>
/// A single decoded IMU reading: three acceleration axes (m/s²) and three angular-rate axes
/// (rad/s). Plain domain value, framework-free (/A20/); the wire DTO
/// (<c>RaceTracker.BuildingBlocks.Contracts.Telemetry.ImuSampleDto</c>) is mapped onto it in
/// the Application layer.
/// </summary>
public sealed record ImuReading(float Ax, float Ay, float Az, float Gx, float Gy, float Gz);
