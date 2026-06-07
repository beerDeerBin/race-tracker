using Microsoft.Extensions.Options;
using MQTTnet;
using RaceTracker.Gateway.Application.Abstractions;
using RaceTracker.Gateway.Application.Configuration;

namespace RaceTracker.Gateway.Infrastructure.Mqtt;

/// <summary>
/// Real MQTT connectivity probe (anti-stub): opens and closes a short-lived MQTTnet
/// connection to the configured broker. Throws if the broker is unreachable.
/// </summary>
public sealed class MqttConnectivityCheck : IMqttConnectivityCheck
{
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);

    private readonly MqttOptions _options;

    public MqttConnectivityCheck(IOptions<GatewayOptions> options)
        => _options = options.Value.Mqtt;

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_connectTimeout);

        using IMqttClient client = new MqttClientFactory().CreateMqttClient();
        var clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId($"gateway-health-{Guid.NewGuid():N}")
            .Build();

        await client.ConnectAsync(clientOptions, timeout.Token);
        await client.DisconnectAsync(cancellationToken: timeout.Token);
    }
}
