using System.Diagnostics.Metrics;

namespace RaceTracker.Management.Application.Observability;

/// <summary>
/// Owns the management <see cref="Meter"/> so the scrape-friendly <c>/metrics</c> endpoint is
/// wired from the grundgerüst on (§8, <c>/A80/</c>). Carries the generic CRUD counter (story 5.3),
/// the device-discovery counter (story 5.4) and the command-dispatch counter (story 5.5). The Api
/// registers <see cref="MeterName"/> with the shared Prometheus exporter.
/// </summary>
public sealed class ManagementMetrics : IDisposable
{
    /// <summary>Meter name registered with OpenTelemetry in the Api composition root.</summary>
    public const string MeterName = "RaceTracker.Management";

    private readonly Meter _meter;
    private readonly Counter<long> _crudOperations;
    private readonly Counter<long> _devicesDiscovered;
    private readonly Counter<long> _statusEvents;
    private readonly Counter<long> _commands;

    public ManagementMetrics()
    {
        _meter = new Meter(MeterName);
        _crudOperations = _meter.CreateCounter<long>(
            "racetracker_management_crud_operations_total",
            unit: "operations",
            description: "Generic CRUD operations performed, tagged by entity type and operation.");
        _devicesDiscovered = _meter.CreateCounter<long>(
            "racetracker_management_devices_discovered_total",
            unit: "devices",
            description: "Unknown device GUIDs lazily registered as pending vehicles via discovery.");
        _statusEvents = _meter.CreateCounter<long>(
            "racetracker_management_status_events_total",
            unit: "messages",
            description: "Status events consumed for discovery, tagged by outcome "
                + "(acked/dead_lettered/requeued).");
        _commands = _meter.CreateCounter<long>(
            "racetracker_management_commands_total",
            unit: "commands",
            description: "Device commands dispatched over MQTT, tagged by command type and outcome "
                + "(sent/failed).");
    }

    /// <summary>
    /// Records a CRUD mutation/read (story 5.3) tagged by <paramref name="entity"/> type name and
    /// <paramref name="operation"/> (e.g. <c>create</c>, <c>update</c>, <c>delete</c>).
    /// </summary>
    public void RecordCrud(string entity, string operation) =>
        _crudOperations.Add(1, new KeyValuePair<string, object?>("entity", entity),
            new KeyValuePair<string, object?>("operation", operation));

    /// <summary>Records a newly discovered device that was lazily registered as pending (story 5.4).</summary>
    public void RecordDiscovered() => _devicesDiscovered.Add(1);

    /// <summary>
    /// Records a consumed status event (story 5.4) tagged by its terminal <paramref name="outcome"/>
    /// (<c>acked</c>, <c>dead_lettered</c> or <c>requeued</c>).
    /// </summary>
    public void RecordStatusEvent(string outcome) =>
        _statusEvents.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    /// <summary>
    /// Records a dispatched device command (story 5.5) tagged by <paramref name="command"/>
    /// (<c>connect</c>/<c>start_run</c>/<c>disconnect</c>/<c>reset</c>) and <paramref name="outcome"/>
    /// (<c>sent</c>/<c>failed</c>).
    /// </summary>
    public void RecordCommand(string command, string outcome) =>
        _commands.Add(1, new KeyValuePair<string, object?>("command", command),
            new KeyValuePair<string, object?>("outcome", outcome));

    public void Dispose() => _meter.Dispose();
}
