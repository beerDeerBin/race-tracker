using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Api.GraphQL;

/// <summary>
/// GraphQL output type for one roll-up bucket (/F53/, /L40/): its <see cref="BucketStartIndex"/>
/// (clients derive <c>t = index / odr_hz</c>, /F54/), the per-axis min/max/avg, and the number of
/// raw samples folded into it. Kept separate from the <see cref="SampleRollupBucket"/> domain model
/// so the wire contract can evolve independently (§6 DTOs).
/// </summary>
public sealed record SampleRollupBucketDto(
    long BucketStartIndex,
    AxisRollupDto Ax, AxisRollupDto Ay, AxisRollupDto Az,
    AxisRollupDto Gx, AxisRollupDto Gy, AxisRollupDto Gz,
    long SampleCount)
{
    /// <summary>Maps a domain roll-up bucket onto its GraphQL DTO.</summary>
    public static SampleRollupBucketDto From(SampleRollupBucket bucket) => new(
        bucket.BucketStartIndex,
        AxisRollupDto.From(bucket.Ax), AxisRollupDto.From(bucket.Ay), AxisRollupDto.From(bucket.Az),
        AxisRollupDto.From(bucket.Gx), AxisRollupDto.From(bucket.Gy), AxisRollupDto.From(bucket.Gz),
        bucket.SampleCount);
}

/// <summary>GraphQL output type for one axis's aggregate (min/max/avg) over a roll-up bucket.</summary>
public sealed record AxisRollupDto(float Min, float Max, double Avg)
{
    /// <summary>Maps a domain axis aggregate onto its GraphQL DTO.</summary>
    public static AxisRollupDto From(AxisRollup axis) => new(axis.Min, axis.Max, axis.Avg);
}
