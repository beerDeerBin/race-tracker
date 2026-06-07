namespace RaceTracker.BuildingBlocks.Contracts.Telemetry;

/// <summary>
/// Normalised device-status event (/S50/, /D30/): the decoded 24-byte <c>MqttStatus_t</c>
/// keepalive (PROTOCOL §5) plus the <see cref="DeviceGuid"/> the gateway lifted from the MQTT
/// topic. Published by the gateway to the <see cref="TelemetryExchanges.Status"/> exchange with
/// the <see cref="DeviceGuid"/> as the routing key, so a consumer can bind to one device or all.
/// Defined once in M2 and consumed unchanged downstream — never re-keyed.
/// </summary>
/// <param name="DeviceGuid">Device GUID (UUID string) — the cross-service correlation key.</param>
/// <param name="UptimeMs">Milliseconds since the device's last fresh boot or RESET.</param>
/// <param name="BatteryMv">Battery voltage in mV; <c>65535</c> means unknown.</param>
/// <param name="BatteryPct">Battery charge 0–100%; <c>255</c> means unknown.</param>
/// <param name="State">Device run state (<see cref="DeviceState"/>).</param>
/// <param name="SampledCount">Samples collected so far in the current run (0 outside a run).</param>
/// <param name="TotalSamples">Samples requested for the current run (0 outside a run).</param>
/// <param name="ErrorCode">64-bit <c>ErrorCodeValue_t</c> bitmask (PROTOCOL §5.1).</param>
/// <param name="ObservedAtUtc">When the gateway observed the message (UTC).</param>
public sealed record StatusEvent(
    string DeviceGuid,
    uint UptimeMs,
    ushort BatteryMv,
    byte BatteryPct,
    DeviceState State,
    uint SampledCount,
    uint TotalSamples,
    ulong ErrorCode,
    DateTimeOffset ObservedAtUtc);
