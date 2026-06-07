namespace RaceTracker.Realtime.Application.Configuration;

/// <summary>
/// Strongly-typed realtime configuration (<c>/A40/</c>), bound once from the
/// <see cref="Section"/> section and consumed via <c>IOptions&lt;RealtimeOptions&gt;</c>.
/// </summary>
public sealed class RealtimeOptions
{
    public const string Section = "Realtime";

    /// <summary>Internal service broker (RabbitMQ) — the status-event source relayed live.</summary>
    public RabbitMqOptions RabbitMq { get; init; } = new();

    /// <summary>Live status-relay consumer topology + flow control (stories 6.2/6.3).</summary>
    public RelayOptions Relay { get; init; } = new();
}

public sealed class RabbitMqOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string VirtualHost { get; init; } = "race-tracker";
    public string Username { get; init; } = "race";
    public string Password { get; init; } = "race";
}

public sealed class RelayOptions
{
    /// <summary>Routing key the relay queue binds with (<c>#</c> = every device's status).</summary>
    public string BindingKey { get; init; } = "#";

    /// <summary>Unacked-message prefetch (QoS) — bounds in-flight relays per consumer.</summary>
    public ushort Prefetch { get; init; } = 64;
}
