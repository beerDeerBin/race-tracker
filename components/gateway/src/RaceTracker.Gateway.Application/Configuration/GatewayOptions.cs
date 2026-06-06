namespace RaceTracker.Gateway.Application.Configuration;

/// <summary>
/// Strongly-typed gateway configuration (<c>/A40/</c>), bound once from the
/// <see cref="Section"/> section and consumed via <c>IOptions&lt;GatewayOptions&gt;</c>.
/// </summary>
public sealed class GatewayOptions
{
    public const string Section = "Gateway";

    /// <summary>Inbound device-telemetry transport (Mosquitto).</summary>
    public MqttOptions Mqtt { get; init; } = new();

    /// <summary>Internal service broker (RabbitMQ) — republish target from 2.3.</summary>
    public RabbitMqOptions RabbitMq { get; init; } = new();
}

public sealed class MqttOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1883;
}

public sealed class RabbitMqOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string VirtualHost { get; init; } = "race-tracker";
    public string Username { get; init; } = "race";
    public string Password { get; init; } = "race";
}
