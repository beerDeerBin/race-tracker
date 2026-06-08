namespace RaceTracker.BuildingBlocks.Contracts.Telemetry;

/// <summary>
/// The RabbitMQ topic-exchange names for normalised telemetry (/S50/), mirroring the two MQTT
/// topic families (<c>rt/&lt;guid&gt;/status</c>, <c>rt/&lt;guid&gt;/data</c>). Both are durable
/// topic exchanges declared idempotently by the gateway producer in story 2.3; the device
/// <c>guid</c> is the routing key, so a consumer binds to a single device (<c>&lt;guid&gt;</c>)
/// or to all devices (<c>#</c>). Named once here and shared by producer and consumers.
/// </summary>
public static class TelemetryExchanges
{
    /// <summary>Topic exchange carrying <see cref="StatusEvent"/> messages.</summary>
    public const string Status = "rt.status";

    /// <summary>Topic exchange carrying <see cref="SampleBatchEvent"/> messages.</summary>
    public const string Data = "rt.data";

    /// <summary>
    /// Topic exchange carrying <see cref="RunMetadataEvent"/> messages — run parameters announced by
    /// the management service at <c>START_RUN</c> (the ODR / time-base source). Routing key is the
    /// device <c>guid</c>, like the other two, so a consumer binds to one device or all (<c>#</c>).
    /// </summary>
    public const string Run = "rt.run";
}
