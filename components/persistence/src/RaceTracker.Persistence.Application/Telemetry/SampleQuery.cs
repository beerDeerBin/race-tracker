namespace RaceTracker.Persistence.Application.Telemetry;

/// <summary>
/// Read filter for the sample query (/F52/): samples of one <see cref="RunId"/>, optionally scoped
/// to a <see cref="DeviceGuid"/> and a sample-index range (<see cref="FromIndex"/>..<see
/// cref="ToIndex"/> — the "Zeitraum" selection, since time is derived as <c>t = index / odr_hz</c>,
/// /F54/). Built through <see cref="Create"/>, which <b>clamps</b> the requested limit into
/// <c>[1, <see cref="MaxLimit"/>]</c> so a full run cannot be returned unbounded by accident.
/// </summary>
public sealed record SampleQuery
{
    /// <summary>Default page size when the caller does not request one.</summary>
    public const int DefaultLimit = 5_000;

    /// <summary>Hard upper bound on a single sample page.</summary>
    public const int MaxLimit = 50_000;

    private SampleQuery(Guid runId, Guid? deviceGuid, long? fromIndex, long? toIndex, int limit)
    {
        RunId = runId;
        DeviceGuid = deviceGuid;
        FromIndex = fromIndex;
        ToIndex = toIndex;
        Limit = limit;
    }

    /// <summary>Run whose samples are requested.</summary>
    public Guid RunId { get; }

    /// <summary>Optional device scope (lets the query target the composite key directly).</summary>
    public Guid? DeviceGuid { get; }

    /// <summary>Inclusive lower bound on the absolute sample index, if set.</summary>
    public long? FromIndex { get; }

    /// <summary>Inclusive upper bound on the absolute sample index, if set.</summary>
    public long? ToIndex { get; }

    /// <summary>Effective page size, clamped to <c>[1, <see cref="MaxLimit"/>]</c>.</summary>
    public int Limit { get; }

    /// <summary>
    /// Builds a query, applying <see cref="DefaultLimit"/> when <paramref name="limit"/> is null and
    /// clamping any value into <c>[1, <see cref="MaxLimit"/>]</c>.
    /// </summary>
    public static SampleQuery Create(
        Guid runId, Guid? deviceGuid = null, long? fromIndex = null, long? toIndex = null,
        int? limit = null)
    {
        int effectiveLimit = limit ?? DefaultLimit;
        effectiveLimit = Math.Clamp(effectiveLimit, 1, MaxLimit);

        return new SampleQuery(runId, deviceGuid, fromIndex, toIndex, effectiveLimit);
    }
}
