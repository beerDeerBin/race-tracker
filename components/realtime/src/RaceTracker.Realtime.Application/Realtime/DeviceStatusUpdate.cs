using RaceTracker.BuildingBlocks.Contracts.Telemetry;

namespace RaceTracker.Realtime.Application.Realtime;

/// <summary>
/// The live device-status push contract (story 6.2, <c>/F60/</c>, <c>/D30/</c>): the subset of a
/// <see cref="StatusEvent"/> a client needs to render a device's live state. Pushed to the
/// per-vehicle SignalR group as the <c>"DeviceStatus"</c> client method. Defined once here as the
/// M6 push contract; the frontend's typed model mirrors it in M7. Reuses the shared
/// <see cref="DeviceState"/> enum so the wire value stays consistent across services.
/// </summary>
/// <param name="DeviceGuid">Device GUID (UUID string) — the cross-service correlation key.</param>
/// <param name="State">Device run state (IDLE/CONNECTED/ACQUIRING).</param>
/// <param name="UptimeMs">Milliseconds since the device's last fresh boot or RESET.</param>
/// <param name="BatteryMv">Battery voltage in mV; <c>65535</c> means unknown.</param>
/// <param name="BatteryPct">Battery charge 0–100%; <c>255</c> means unknown.</param>
/// <param name="ErrorCode">64-bit error-code bitmask (PROTOCOL §5.1).</param>
/// <param name="ObservedAtUtc">When the gateway observed the source status (UTC).</param>
public sealed record DeviceStatusUpdate(
    string DeviceGuid,
    DeviceState State,
    uint UptimeMs,
    ushort BatteryMv,
    byte BatteryPct,
    ulong ErrorCode,
    DateTimeOffset ObservedAtUtc);
