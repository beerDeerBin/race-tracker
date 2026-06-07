namespace RaceTracker.Persistence.Domain.Telemetry;

/// <summary>
/// A validated batch of run samples ready to upsert. Built through <see cref="Create"/>, which
/// enforces the domain invariants and throws <see cref="TelemetryValidationException"/> on any
/// violation — so an invalid message is rejected at the boundary (§7 validation) and
/// dead-lettered rather than written. Telemetry is keyed by <see cref="DeviceGuid"/> +
/// <see cref="RunId"/> + sample index; each sample carries its <b>absolute</b> index
/// (<c>StartOffset + position</c>) so batches upsert independently and idempotently (/F54/).
/// </summary>
public sealed class SampleBatch
{
    /// <summary>Max samples per batch (PROTOCOL §6 / /L10/: data batches hold ≤ 32 samples).</summary>
    public const int MaxBatchSize = 32;

    private SampleBatch(
        Guid deviceGuid, Guid runId, long startOffset,
        IReadOnlyList<RunSample> samples, DateTimeOffset observedAtUtc)
    {
        DeviceGuid = deviceGuid;
        RunId = runId;
        StartOffset = startOffset;
        Samples = samples;
        ObservedAtUtc = observedAtUtc;
    }

    /// <summary>Device GUID (cross-service correlation key) — the run's <c>device_guid</c>.</summary>
    public Guid DeviceGuid { get; }

    /// <summary>Run UUID — the run's <c>run_id</c>.</summary>
    public Guid RunId { get; }

    /// <summary>Absolute index of the first sample in this batch.</summary>
    public long StartOffset { get; }

    /// <summary>The samples, each at its absolute run index, in run order.</summary>
    public IReadOnlyList<RunSample> Samples { get; }

    /// <summary>When the gateway observed the batch (drives run start/end timestamps).</summary>
    public DateTimeOffset ObservedAtUtc { get; }

    /// <summary>
    /// Validates and builds a batch from the wire fields. Throws
    /// <see cref="TelemetryValidationException"/> if: the GUID/runId are not UUIDs; the start
    /// offset is negative; the readings are empty; the declared count disagrees with the readings;
    /// or the batch exceeds <see cref="MaxBatchSize"/>.
    /// </summary>
    public static SampleBatch Create(
        string deviceGuid, string runId, long startOffset, int declaredCount,
        IReadOnlyList<ImuReading> readings, DateTimeOffset observedAtUtc)
    {
        if (!Guid.TryParse(deviceGuid, out Guid guid))
        {
            throw new TelemetryValidationException(
                $"Device GUID '{deviceGuid}' is not a valid UUID.");
        }

        if (!Guid.TryParse(runId, out Guid run))
        {
            throw new TelemetryValidationException($"Run id '{runId}' is not a valid UUID.");
        }

        if (startOffset < 0)
        {
            throw new TelemetryValidationException(
                $"Start offset {startOffset} is negative.");
        }

        if (readings is null || readings.Count == 0)
        {
            throw new TelemetryValidationException("Sample batch carries no readings.");
        }

        if (declaredCount != readings.Count)
        {
            throw new TelemetryValidationException(
                $"Declared count {declaredCount} does not match {readings.Count} readings.");
        }

        if (readings.Count > MaxBatchSize)
        {
            throw new TelemetryValidationException(
                $"Batch of {readings.Count} exceeds the maximum of {MaxBatchSize} samples.");
        }

        var samples = new RunSample[readings.Count];
        for (int i = 0; i < readings.Count; i++)
        {
            samples[i] = new RunSample(startOffset + i, readings[i]);
        }

        return new SampleBatch(guid, run, startOffset, samples, observedAtUtc);
    }
}
