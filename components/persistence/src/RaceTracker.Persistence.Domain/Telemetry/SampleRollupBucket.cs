namespace RaceTracker.Persistence.Domain.Telemetry;

/// <summary>
/// Read-side projection of one roll-up bucket (/F53/, /L40/) from the <c>samples_rollup</c>
/// continuous aggregate: the bucket's <see cref="BucketStartIndex"/> (the floored sample index that
/// opens the window) plus the min/max/avg of each IMU axis over the bucket and the
/// <see cref="SampleCount"/> of raw samples in it. There is no timestamp — time is derived as
/// <c>t = BucketStartIndex / odr_hz</c> (/F54/), the same convention as raw samples.
/// </summary>
public sealed record SampleRollupBucket(
    long BucketStartIndex,
    AxisRollup Ax, AxisRollup Ay, AxisRollup Az,
    AxisRollup Gx, AxisRollup Gy, AxisRollup Gz,
    long SampleCount);

/// <summary>
/// Per-axis aggregate over a roll-up bucket: <see cref="Min"/>/<see cref="Max"/> keep the axis unit
/// (<c>real</c>) while <see cref="Avg"/> is the double-precision mean (Postgres <c>avg(real)</c>).
/// </summary>
public sealed record AxisRollup(float Min, float Max, double Avg);
