using Microsoft.Extensions.Diagnostics.HealthChecks;
using RaceTracker.Management.Application.Abstractions;

namespace RaceTracker.Management.Infrastructure.Health;

/// <summary>Readiness check that reports the internal message broker's reachability (story 5.4).</summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IRabbitMqConnectivityCheck _connectivity;

    public RabbitMqHealthCheck(IRabbitMqConnectivityCheck connectivity) => _connectivity = connectivity;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectivity.CheckAsync(cancellationToken);
            return HealthCheckResult.Healthy("RabbitMQ reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ unreachable.", ex);
        }
    }
}
