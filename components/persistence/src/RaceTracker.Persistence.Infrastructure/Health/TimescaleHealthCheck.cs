using Microsoft.Extensions.Diagnostics.HealthChecks;
using RaceTracker.Persistence.Application.Abstractions;

namespace RaceTracker.Persistence.Infrastructure.Health;

/// <summary>Readiness check that reports the TimescaleDB store's reachability.</summary>
public sealed class TimescaleHealthCheck : IHealthCheck
{
    private readonly ITimescaleConnectivityCheck _connectivity;

    public TimescaleHealthCheck(ITimescaleConnectivityCheck connectivity) => _connectivity = connectivity;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectivity.CheckAsync(cancellationToken);
            return HealthCheckResult.Healthy("TimescaleDB reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("TimescaleDB unreachable.", ex);
        }
    }
}
