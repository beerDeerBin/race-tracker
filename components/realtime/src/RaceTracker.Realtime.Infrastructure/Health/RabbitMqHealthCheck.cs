using Microsoft.Extensions.Diagnostics.HealthChecks;
using RaceTracker.Realtime.Application.Abstractions;

namespace RaceTracker.Realtime.Infrastructure.Health;

/// <summary>Readiness check that reports the internal RabbitMQ broker's reachability.</summary>
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
            return HealthCheckResult.Healthy("RabbitMQ broker reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ broker unreachable.", ex);
        }
    }
}
