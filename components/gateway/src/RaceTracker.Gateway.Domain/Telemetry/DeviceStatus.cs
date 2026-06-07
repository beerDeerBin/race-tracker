namespace RaceTracker.Gateway.Domain.Telemetry;

/// <summary>
/// A decoded device status keepalive — the typed form of the packed 24-byte
/// <c>MqttStatus_t</c> (PROTOCOL §5). Payload-only: the device <c>guid</c> is carried by
/// the topic path (<c>rt/&lt;guid&gt;/status</c>), not the payload, and is attached by the
/// ingestion handler — so this model stays free of transport/contract concerns (the
/// RabbitMQ message contract that combines guid + payload arrives in story 2.3).
/// </summary>
public sealed record DeviceStatus(
    uint UptimeMs,
    ushort BatteryMv,
    byte BatteryPct,
    DeviceState State,
    uint SampledCount,
    uint TotalSamples,
    ulong ErrorCode);
