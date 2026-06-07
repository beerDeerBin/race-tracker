using Microsoft.Extensions.Diagnostics.HealthChecks;
using RaceTracker.Gateway.Application.Abstractions;

namespace RaceTracker.Gateway.Infrastructure.Health;

/// <summary>Readiness check that reports the inbound MQTT broker's reachability.</summary>
public sealed class MqttHealthCheck : IHealthCheck
{
    private readonly IMqttConnectivityCheck _connectivity;

    public MqttHealthCheck(IMqttConnectivityCheck connectivity) => _connectivity = connectivity;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectivity.CheckAsync(cancellationToken);
            return HealthCheckResult.Healthy("MQTT broker reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MQTT broker unreachable.", ex);
        }
    }
}
