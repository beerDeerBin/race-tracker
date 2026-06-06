using RaceTracker.Persistence.Application.Telemetry;
using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Application.Abstractions;

/// <summary>
/// Read-side port (/F51/, /F52/) for querying stored telemetry. Deliberately <b>separate</b> from
/// the write-side <see cref="ITelemetryRepository"/> — the read and write paths are split
/// CQRS-style and never share an interface. Defined in Application; implemented by the Timescale
/// read adapter and surfaced through the GraphQL query type.
/// </summary>
public interface ITelemetryReadStore
{
    /// <summary>Returns the runs recorded for a device (newest activity first), keyed by guid.</summary>
    Task<IReadOnlyList<RunMetadata>> GetRunsAsync(Guid deviceGuid, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a run's samples in ascending index order, scoped and paged by <paramref name="query"/>.
    /// </summary>
    Task<IReadOnlyList<RunSample>> GetSamplesAsync(
        SampleQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a run's pre-aggregated roll-up buckets (/F53/, /L40/) in ascending bucket-start order,
    /// read from the continuous aggregate, scoped and paged by <paramref name="query"/>.
    /// </summary>
    Task<IReadOnlyList<SampleRollupBucket>> GetRunRollupAsync(
        RollupQuery query, CancellationToken cancellationToken);
}
