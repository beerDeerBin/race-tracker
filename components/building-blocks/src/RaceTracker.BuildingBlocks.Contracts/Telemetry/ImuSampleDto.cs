namespace RaceTracker.BuildingBlocks.Contracts.Telemetry;

/// <summary>
/// One normalised IMU reading inside a <see cref="SampleBatchEvent"/> (/D50/): three
/// acceleration axes (m/s²) and three angular rates (rad/s). No timestamp — the time base is
/// derived downstream from the run's ODR (<c>t = index / odr_hz</c>), where the sample's
/// absolute index is <see cref="SampleBatchEvent.StartOffset"/> + its position in the batch.
/// </summary>
public sealed record ImuSampleDto(float Ax, float Ay, float Az, float Gx, float Gy, float Gz);
