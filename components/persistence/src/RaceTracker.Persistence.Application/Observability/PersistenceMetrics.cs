using System.Diagnostics.Metrics;

namespace RaceTracker.Persistence.Application.Observability;

/// <summary>
/// Scrape-friendly write-path counters (§8): how many samples were written and how sample
/// batches were resolved (acked, dead-lettered as poison, or requeued as transient). Owns the
/// persistence <see cref="Meter"/>; the Api exposes it through the shared Prometheus endpoint.
/// </summary>
public sealed class PersistenceMetrics : IDisposable
{
    /// <summary>Meter name registered with OpenTelemetry in the Api composition root.</summary>
    public const string MeterName = "RaceTracker.Persistence";

    private readonly Meter _meter;
    private readonly Counter<long> _samplesWritten;
    private readonly Counter<long> _batchesAcked;
    private readonly Counter<long> _batchesDeadLettered;
    private readonly Counter<long> _batchesRequeued;

    public PersistenceMetrics()
    {
        _meter = new Meter(MeterName);
        _samplesWritten = _meter.CreateCounter<long>(
            "persistence.samples.written", unit: "{sample}",
            description: "Samples newly inserted into Timescale (redelivered duplicates excluded).");
        _batchesAcked = _meter.CreateCounter<long>(
            "persistence.batches.acked", unit: "{batch}",
            description: "Sample batches persisted and acknowledged.");
        _batchesDeadLettered = _meter.CreateCounter<long>(
            "persistence.batches.deadlettered", unit: "{batch}",
            description: "Sample batches rejected without requeue (parse/validation) → dead-letter.");
        _batchesRequeued = _meter.CreateCounter<long>(
            "persistence.batches.requeued", unit: "{batch}",
            description: "Sample batches rejected with requeue after a transient failure.");
    }

    /// <summary>Counts <paramref name="count"/> newly inserted samples for an acked batch.</summary>
    public void SamplesWritten(long count) => _samplesWritten.Add(count);

    /// <summary>Counts a successfully persisted + acked batch.</summary>
    public void BatchAcked() => _batchesAcked.Add(1);

    /// <summary>Counts a poison batch dead-lettered (rejected without requeue).</summary>
    public void BatchDeadLettered() => _batchesDeadLettered.Add(1);

    /// <summary>Counts a batch requeued after a transient failure.</summary>
    public void BatchRequeued() => _batchesRequeued.Add(1);

    public void Dispose() => _meter.Dispose();
}
