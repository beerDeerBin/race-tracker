using Microsoft.Extensions.Diagnostics.HealthChecks;
using RaceTracker.Management.Application.Abstractions;

namespace RaceTracker.Management.Infrastructure.Health;

/// <summary>Readiness check that reports the device MQTT broker's reachability (story 5.5).</summary>
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
            return HealthCheckResult.Healthy("MQTT reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MQTT unreachable.", ex);
        }
    }
}
