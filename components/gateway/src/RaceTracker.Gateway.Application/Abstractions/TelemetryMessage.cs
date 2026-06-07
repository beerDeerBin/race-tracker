namespace RaceTracker.Gateway.Application.Abstractions;

/// <summary>
/// A raw telemetry message received off the device transport: the MQTT
/// <paramref name="Topic"/> (<c>rt/&lt;guid&gt;/&lt;leaf&gt;</c>) and the undecoded
/// <paramref name="Payload"/> bytes. Transport-agnostic so the ingestion handler can be
/// unit-tested without MQTTnet.
/// </summary>
public sealed record TelemetryMessage(string Topic, byte[] Payload);
