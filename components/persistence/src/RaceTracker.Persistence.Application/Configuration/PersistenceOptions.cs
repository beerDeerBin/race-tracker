namespace RaceTracker.Persistence.Application.Configuration;

/// <summary>
/// Strongly-typed persistence configuration (<c>/A40/</c>), bound once from the
/// <see cref="Section"/> section and consumed via <c>IOptions&lt;PersistenceOptions&gt;</c>.
/// </summary>
public sealed class PersistenceOptions
{
    public const string Section = "Persistence";

    /// <summary>Internal service broker (RabbitMQ) — telemetry source consumed from 3.3.</summary>
    public RabbitMqOptions RabbitMq { get; init; } = new();

    /// <summary>Time-series store (TimescaleDB/PostgreSQL) for samples + run metadata.</summary>
    public TimescaleOptions Timescale { get; init; } = new();

    /// <summary>Sample-batch consumer topology + flow control (story 3.3).</summary>
    public ConsumerOptions Consumer { get; init; } = new();

    /// <summary>Derived dead-reckoning trajectory projection (story 4.3).</summary>
    public TrajectoryOptions Trajectory { get; init; } = new();
}

public sealed class RabbitMqOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string VirtualHost { get; init; } = "race-tracker";
    public string Username { get; init; } = "race";
    public string Password { get; init; } = "race";
}

public sealed class ConsumerOptions
{
    /// <summary>Durable work queue bound to the <c>rt.data</c> exchange.</summary>
    public string Queue { get; init; } = "rt.persistence.samples";

    /// <summary>Routing key the queue binds with (<c>#</c> = every device).</summary>
    public string BindingKey { get; init; } = "#";

    /// <summary>Dead-letter exchange for poison (parse/validation-failed) messages.</summary>
    public string DeadLetterExchange { get; init; } = "rt.persistence.dlx";

    /// <summary>Dead-letter queue bound to <see cref="DeadLetterExchange"/>.</summary>
    public string DeadLetterQueue { get; init; } = "rt.persistence.dlq";

    /// <summary>Unacked-message prefetch (QoS) — bounds in-flight work per consumer.</summary>
    public ushort Prefetch { get; init; } = 32;
}

public sealed class TrajectoryOptions
{
    /// <summary>
    /// ODR (Hz) used to derive the time base and integration step when a run carries no
    /// <c>odr_hz</c> yet (PROTOCOL default 104 Hz; replaced once 5.x plumbs the real ODR).
    /// </summary>
    public double DefaultOdrHz { get; init; } = 104;

    /// <summary>How often the projection worker scans for stale runs.</summary>
    public int RebuildIntervalSeconds { get; init; } = 3;

    /// <summary>
    /// How long a run must be quiet (no new batch upsert) before it is rebuilt, so the end-of-run
    /// batch burst coalesces into a single recompute.
    /// </summary>
    public int SettleSeconds { get; init; } = 2;
}

public sealed class TimescaleOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5432;
    public string Database { get; init; } = "racetracker";
    public string Username { get; init; } = "race";
    public string Password { get; init; } = "race";
}
