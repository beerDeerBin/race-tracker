using System.Diagnostics.Metrics;

namespace RaceTracker.Management.Application.Observability;

/// <summary>
/// Owns the management <see cref="Meter"/> so the scrape-friendly <c>/metrics</c> endpoint is
/// wired from the grundgerüst on (§8, <c>/A80/</c>). Carries the generic CRUD counter (story 5.3);
/// command-dispatch counters (story 5.5) are added here as those use cases land. The Api registers
/// <see cref="MeterName"/> with the shared Prometheus exporter.
/// </summary>
public sealed class ManagementMetrics : IDisposable
{
    /// <summary>Meter name registered with OpenTelemetry in the Api composition root.</summary>
    public const string MeterName = "RaceTracker.Management";

    private readonly Meter _meter;
    private readonly Counter<long> _crudOperations;

    public ManagementMetrics()
    {
        _meter = new Meter(MeterName);
        _crudOperations = _meter.CreateCounter<long>(
            "racetracker_management_crud_operations_total",
            unit: "operations",
            description: "Generic CRUD operations performed, tagged by entity type and operation.");
    }

    /// <summary>
    /// Records a CRUD mutation/read (story 5.3) tagged by <paramref name="entity"/> type name and
    /// <paramref name="operation"/> (e.g. <c>create</c>, <c>update</c>, <c>delete</c>).
    /// </summary>
    public void RecordCrud(string entity, string operation) =>
        _crudOperations.Add(1, new KeyValuePair<string, object?>("entity", entity),
            new KeyValuePair<string, object?>("operation", operation));

    public void Dispose() => _meter.Dispose();
}
