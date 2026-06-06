using System.Diagnostics.Metrics;

namespace RaceTracker.Management.Application.Observability;

/// <summary>
/// Owns the management <see cref="Meter"/> so the scrape-friendly <c>/metrics</c> endpoint is
/// wired from the grundgerüst on (§8, <c>/A80/</c>). The 5.1 service has no domain operations
/// yet, so it carries no instruments; CRUD counters (story 5.3) and command-dispatch counters
/// (story 5.5) are added here as those use cases land. The Api registers <see cref="MeterName"/>
/// with the shared Prometheus exporter.
/// </summary>
public sealed class ManagementMetrics : IDisposable
{
    /// <summary>Meter name registered with OpenTelemetry in the Api composition root.</summary>
    public const string MeterName = "RaceTracker.Management";

    private readonly Meter _meter;

    public ManagementMetrics() => _meter = new Meter(MeterName);

    public void Dispose() => _meter.Dispose();
}
