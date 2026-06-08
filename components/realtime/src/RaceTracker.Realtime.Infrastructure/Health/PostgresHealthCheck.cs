using Microsoft.Extensions.Diagnostics.HealthChecks;
using RaceTracker.Realtime.Application.Abstractions;

namespace RaceTracker.Realtime.Infrastructure.Health;

/// <summary>Readiness check that reports the outbox PostgreSQL store's reachability (story 8.3).</summary>
public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly IPostgresConnectivityCheck _connectivity;

    public PostgresHealthCheck(IPostgresConnectivityCheck connectivity) => _connectivity = connectivity;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectivity.CheckAsync(cancellationToken);
            return HealthCheckResult.Healthy("Outbox PostgreSQL reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Outbox PostgreSQL unreachable.", ex);
        }
    }
}
