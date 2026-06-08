using Microsoft.Extensions.Diagnostics.HealthChecks;
using RaceTracker.Realtime.Application.Abstractions;

namespace RaceTracker.Realtime.Infrastructure.Health;

/// <summary>Readiness check that reports the Redis cache's reachability (story 8.2).</summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IRedisConnectivityCheck _connectivity;

    public RedisHealthCheck(IRedisConnectivityCheck connectivity) => _connectivity = connectivity;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectivity.CheckAsync(cancellationToken);
            return HealthCheckResult.Healthy("Redis reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis unreachable.", ex);
        }
    }
}
