using Microsoft.Extensions.Diagnostics.HealthChecks;
using RaceTracker.Management.Application.Abstractions;

namespace RaceTracker.Management.Infrastructure.Health;

/// <summary>Readiness check that reports the MongoDB store's reachability.</summary>
public sealed class MongoHealthCheck : IHealthCheck
{
    private readonly IMongoConnectivityCheck _connectivity;

    public MongoHealthCheck(IMongoConnectivityCheck connectivity) => _connectivity = connectivity;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectivity.CheckAsync(cancellationToken);
            return HealthCheckResult.Healthy("MongoDB reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB unreachable.", ex);
        }
    }
}
