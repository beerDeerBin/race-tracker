using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Telemetry;
using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Api.GraphQL;

/// <summary>
/// GraphQL query root (/F52/, /S30/) for the persistence read path. Thin transport edge: each
/// resolver delegates to the <see cref="ITelemetryReadStore"/> read port (CQRS read half, /F51/)
/// and maps the domain result onto a DTO. Field selection is inherent to GraphQL; range ("Zeitraum")
/// selection is by sample index. Registered via <c>AddQueryType&lt;TelemetryQuery&gt;()</c>.
/// </summary>
public sealed class TelemetryQuery
{
    /// <summary>Runs recorded for a device, keyed by its cross-service correlation guid.</summary>
    public async Task<IReadOnlyList<RunDto>> GetRunsAsync(
        Guid deviceGuid,
        [Service] ITelemetryReadStore readStore,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<RunMetadata> runs = await readStore.GetRunsAsync(deviceGuid, cancellationToken);
        return [.. runs.Select(RunDto.From)];
    }

    /// <summary>
    /// Samples of a run in ascending index order, optionally scoped to a device and a sample-index
    /// range, paged by <paramref name="limit"/> (clamped by <see cref="SampleQuery"/>).
    /// </summary>
    public async Task<IReadOnlyList<SampleDto>> GetSamplesAsync(
        Guid runId,
        [Service] ITelemetryReadStore readStore,
        CancellationToken cancellationToken,
        Guid? deviceGuid = null,
        long? fromIndex = null,
        long? toIndex = null,
        int? limit = null)
    {
        SampleQuery query = SampleQuery.Create(runId, deviceGuid, fromIndex, toIndex, limit);
        IReadOnlyList<RunSample> samples = await readStore.GetSamplesAsync(query, cancellationToken);
        return [.. samples.Select(SampleDto.From)];
    }

    /// <summary>
    /// Pre-aggregated roll-up buckets of a run (/F53/, /L40/) in ascending bucket-start order — the
    /// down-sampled dashboard view computed by the continuous aggregate, not over raw rows.
    /// Optionally scoped to a device and a bucket-start range, paged by <paramref name="limit"/>
    /// (clamped by <see cref="RollupQuery"/>).
    /// </summary>
    public async Task<IReadOnlyList<SampleRollupBucketDto>> GetRunRollupAsync(
        Guid runId,
        [Service] ITelemetryReadStore readStore,
        CancellationToken cancellationToken,
        Guid? deviceGuid = null,
        long? fromIndex = null,
        long? toIndex = null,
        int? limit = null)
    {
        RollupQuery query = RollupQuery.Create(runId, deviceGuid, fromIndex, toIndex, limit);
        IReadOnlyList<SampleRollupBucket> buckets =
            await readStore.GetRunRollupAsync(query, cancellationToken);
        return [.. buckets.Select(SampleRollupBucketDto.From)];
    }
}
