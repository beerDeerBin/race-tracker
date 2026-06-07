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

    /// <summary>Key-value cache (Redis) for notification TTL idempotency (story 8.2).</summary>
    public RedisOptions Redis { get; init; } = new();

    /// <summary>Tunables for the stateful story-8.4 rules (run-finished / offline thresholds).</summary>
    public RulesOptions Rules { get; init; } = new();
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

public sealed class RedisOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 6379;

    /// <summary>
    /// Debounce window (seconds) for rule notifications (<c>/F72/</c>): the same condition on
    /// the same device notifies at most once per window. Default 5 minutes.
    /// </summary>
    public int NotificationTtlSeconds { get; init; } = 300;
}

public sealed class RulesOptions
{
    /// <summary>
    /// Seconds without a status keepalive after which a device counts as offline (story 8.4,
    /// <c>/O70/</c>). The firmware publishes every ~5 s; the default 15 s = three missed keepalives.
    /// </summary>
    public int OfflineThresholdSeconds { get; init; } = 15;

    /// <summary>How often (seconds) the offline monitor sweeps last-seen timestamps. Default 5 s.</summary>
    public int OfflineSweepSeconds { get; init; } = 5;
}
