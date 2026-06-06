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
}

public sealed class RabbitMqOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string VirtualHost { get; init; } = "race-tracker";
    public string Username { get; init; } = "race";
    public string Password { get; init; } = "race";
}

public sealed class TimescaleOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5432;
    public string Database { get; init; } = "racetracker";
    public string Username { get; init; } = "race";
    public string Password { get; init; } = "race";
}
