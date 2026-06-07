namespace RaceTracker.Persistence.Application.Telemetry;

/// <summary>
/// Read filter for the trajectory query (/F52/, story 4.3): the derived points of one
/// <see cref="RunId"/>, optionally scoped to a <see cref="DeviceGuid"/> and a sample-index range
/// (<see cref="FromIndex"/>..<see cref="ToIndex"/>, the "Zeitraum" selection since time is
/// <c>t = index / odr_hz</c>, /F54/). A <see cref="Stride"/> &gt; 1 downsamples large runs (every
/// <c>stride</c>-th point; index 0 is always on a stride boundary so the origin point is kept).
/// Built through <see cref="Create"/>, which <b>clamps</b> the limit into
/// <c>[1, <see cref="MaxLimit"/>]</c> and the stride to at least 1. Mirrors
/// <see cref="SampleQuery"/>/<see cref="RollupQuery"/>.
/// </summary>
public sealed record TrajectoryQuery
{
    /// <summary>Default page size when the caller does not request one (covers a standard run in full).</summary>
    public const int DefaultLimit = 10_000;

    /// <summary>Hard upper bound on a single trajectory page.</summary>
    public const int MaxLimit = 100_000;

    private TrajectoryQuery(
        Guid runId, Guid? deviceGuid, long? fromIndex, long? toIndex, int stride, int limit)
    {
        RunId = runId;
        DeviceGuid = deviceGuid;
        FromIndex = fromIndex;
        ToIndex = toIndex;
        Stride = stride;
        Limit = limit;
    }

    /// <summary>Run whose trajectory is requested.</summary>
    public Guid RunId { get; }

    /// <summary>Optional device scope (lets the query target the composite key directly).</summary>
    public Guid? DeviceGuid { get; }

    /// <summary>Inclusive lower bound on the sample index, if set.</summary>
    public long? FromIndex { get; }

    /// <summary>Inclusive upper bound on the sample index, if set.</summary>
    public long? ToIndex { get; }

    /// <summary>Downsampling stride (every <c>stride</c>-th point); always at least 1.</summary>
    public int Stride { get; }

    /// <summary>Effective page size, clamped to <c>[1, <see cref="MaxLimit"/>]</c>.</summary>
    public int Limit { get; }

    /// <summary>
    /// Builds a query, applying <see cref="DefaultLimit"/> when <paramref name="limit"/> is null,
    /// clamping the limit into <c>[1, <see cref="MaxLimit"/>]</c> and the stride up to at least 1.
    /// </summary>
    public static TrajectoryQuery Create(
        Guid runId, Guid? deviceGuid = null, long? fromIndex = null, long? toIndex = null,
        int? stride = null, int? limit = null)
    {
        int effectiveStride = Math.Max(1, stride ?? 1);

        int effectiveLimit = limit ?? DefaultLimit;
        effectiveLimit = Math.Clamp(effectiveLimit, 1, MaxLimit);

        return new TrajectoryQuery(runId, deviceGuid, fromIndex, toIndex, effectiveStride, effectiveLimit);
    }
}
