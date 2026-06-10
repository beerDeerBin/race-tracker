namespace RaceTracker.Management.Application.Configuration;

/// <summary>
/// Strongly-typed management configuration (<c>/A40/</c>), bound once from the
/// <see cref="Section"/> section and consumed via <c>IOptions&lt;ManagementOptions&gt;</c>.
/// </summary>
public sealed class ManagementOptions
{
    public const string Section = "Management";

    /// <summary>Document store (MongoDB) for the management domain entities (User/Vehicle).</summary>
    public MongoOptions Mongo { get; init; } = new();

    /// <summary>Internal service broker (RabbitMQ) — status-event source consumed for discovery (5.4).</summary>
    public RabbitMqOptions RabbitMq { get; init; } = new();

    /// <summary>Device transport (MQTT/Mosquitto) — command-dispatch target on <c>rt/&lt;guid&gt;/cmd</c> (5.5).</summary>
    public MqttOptions Mqtt { get; init; } = new();

    /// <summary>Status-event consumer topology + flow control for device discovery (5.4).</summary>
    public DiscoveryOptions Discovery { get; init; } = new();

    /// <summary>Vehicle gallery image constraints (allowed types + size cap).</summary>
    public ImageOptions Images { get; init; } = new();
}

/// <summary>
/// Constraints for vehicle gallery image uploads. Bound under <c>Management:Images</c> (Options
/// pattern, <c>/A40/</c>) and enforced once in the upload use case so the rule lives in one place.
/// </summary>
public sealed class ImageOptions
{
    /// <summary>Maximum accepted size of a single image in bytes (default 5 MiB).</summary>
    public long MaxBytes { get; init; } = 5L * 1024 * 1024;

    /// <summary>Accepted image MIME types; an upload with any other content type is rejected.</summary>
    public string[] AllowedContentTypes { get; init; } =
        ["image/png", "image/jpeg", "image/webp", "image/gif"];
}

/// <summary>
/// MQTT (Mosquitto) connection settings, mirroring the gateway's so both services point at the same
/// device broker. Management connects to it as a <b>producer</b> to publish commands (5.5).
/// </summary>
public sealed class MqttOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1883;
}

/// <summary>
/// MongoDB connection settings. Credentials are optional: the local dev stack runs Mongo
/// without auth, so <see cref="Username"/>/<see cref="Password"/> stay empty and are only
/// woven into the connection string when set (see <c>MongoConnectionString</c>).
/// </summary>
public sealed class MongoOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 27017;
    public string Database { get; init; } = "racetracker";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
}

/// <summary>
/// RabbitMQ connection settings, mirroring the producer/consumer settings used by the gateway and
/// persistence services so all services point at the same internal broker/vhost.
/// </summary>
public sealed class RabbitMqOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string VirtualHost { get; init; } = "race-tracker";
    public string Username { get; init; } = "race";
    public string Password { get; init; } = "race";
}

/// <summary>
/// Topology + flow control for the status-event consumer that backs device discovery (5.4). Binds a
/// durable work queue to the <c>rt.status</c> topic exchange; poison messages dead-letter rather than
/// requeue (§8 Zuverlässigkeit).
/// </summary>
public sealed class DiscoveryOptions
{
    /// <summary>Durable work queue bound to the <c>rt.status</c> exchange.</summary>
    public string Queue { get; init; } = "rt.management.discovery";

    /// <summary>Routing key the queue binds with (<c>#</c> = every device).</summary>
    public string BindingKey { get; init; } = "#";

    /// <summary>Dead-letter exchange for poison (parse/validation-failed) messages.</summary>
    public string DeadLetterExchange { get; init; } = "rt.management.dlx";

    /// <summary>Dead-letter queue bound to <see cref="DeadLetterExchange"/>.</summary>
    public string DeadLetterQueue { get; init; } = "rt.management.dlq";

    /// <summary>Unacked-message prefetch (QoS) — bounds in-flight work per consumer.</summary>
    public ushort Prefetch { get; init; } = 32;
}
