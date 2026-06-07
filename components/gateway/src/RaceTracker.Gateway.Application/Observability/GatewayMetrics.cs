using System.Diagnostics.Metrics;

namespace RaceTracker.Gateway.Application.Observability;

/// <summary>
/// Scrape-friendly ingestion counters (§8): how many telemetry messages were received,
/// successfully decoded, republished to RabbitMQ, and dropped/failed. Owns the gateway
/// <see cref="Meter"/>; the Api exposes it through the shared Prometheus endpoint.
/// </summary>
public sealed class GatewayMetrics : IDisposable
{
    /// <summary>Meter name registered with OpenTelemetry in the Api composition root.</summary>
    public const string MeterName = "RaceTracker.Gateway";

    private readonly Meter _meter;
    private readonly Counter<long> _received;
    private readonly Counter<long> _decoded;
    private readonly Counter<long> _published;
    private readonly Counter<long> _failed;

    public GatewayMetrics()
    {
        _meter = new Meter(MeterName);
        _received = _meter.CreateCounter<long>(
            "gateway.messages.received", unit: "{message}",
            description: "Telemetry messages received off MQTT.");
        _decoded = _meter.CreateCounter<long>(
            "gateway.messages.decoded", unit: "{message}",
            description: "Telemetry messages successfully decoded.");
        _published = _meter.CreateCounter<long>(
            "gateway.messages.published", unit: "{message}",
            description: "Telemetry messages republished to RabbitMQ.");
        _failed = _meter.CreateCounter<long>(
            "gateway.messages.failed", unit: "{message}",
            description: "Telemetry messages dropped as malformed or failed to publish.");
    }

    /// <summary>Counts a received message, tagged by its topic <paramref name="leaf"/>.</summary>
    public void Received(string leaf) => _received.Add(1, new KeyValuePair<string, object?>("leaf", leaf));

    /// <summary>Counts a successfully decoded message, tagged by its <paramref name="type"/>.</summary>
    public void Decoded(string type) => _decoded.Add(1, new KeyValuePair<string, object?>("type", type));

    /// <summary>Counts a message republished to RabbitMQ, tagged by its <paramref name="type"/>.</summary>
    public void Published(string type) => _published.Add(1, new KeyValuePair<string, object?>("type", type));

    /// <summary>Counts a dropped/failed message, tagged by <paramref name="reason"/>.</summary>
    public void Failed(string reason) => _failed.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void Dispose() => _meter.Dispose();
}
