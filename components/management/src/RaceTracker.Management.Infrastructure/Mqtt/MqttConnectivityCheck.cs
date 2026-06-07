using Microsoft.Extensions.Options;
using MQTTnet;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Configuration;

namespace RaceTracker.Management.Infrastructure.Mqtt;

/// <summary>
/// Real MQTT connectivity probe (anti-stub): opens and closes a short-lived MQTTnet connection to the
/// configured device broker. Throws if the broker is unreachable. Backs the readiness check after
/// Management gained an MQTT dependency in 5.5 (command dispatch). Mirrors the gateway's probe.
/// </summary>
public sealed class MqttConnectivityCheck : IMqttConnectivityCheck
{
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);

    private readonly MqttOptions _options;

    public MqttConnectivityCheck(IOptions<ManagementOptions> options)
        => _options = options.Value.Mqtt;

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_connectTimeout);

        using IMqttClient client = new MqttClientFactory().CreateMqttClient();
        var clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId($"management-health-{Guid.NewGuid():N}")
            .Build();

        await client.ConnectAsync(clientOptions, timeout.Token);
        await client.DisconnectAsync(cancellationToken: timeout.Token);
    }
}
