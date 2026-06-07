using System.Text.Json.Serialization;

namespace RaceTracker.BuildingBlocks.Contracts.Telemetry;

/// <summary>
/// Device run state carried in a <see cref="StatusEvent"/>. Mirrors the firmware
/// <c>SystemState_t</c> values (PROTOCOL §5) but is a contract-level type, decoupled from any
/// producer's domain enum so consumers depend only on this shared assembly. Serialized by name
/// (<c>"Idle"</c>/<c>"Connected"</c>/<c>"Acquiring"</c>) for readable, language-agnostic JSON —
/// the converter is pinned on the type so producer and every consumer agree without extra config.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DeviceState>))]
public enum DeviceState
{
    /// <summary>Powered, connected to the broker, no session (firmware <c>0</c>).</summary>
    Idle = 0,

    /// <summary>Session established, ready to start a run (firmware <c>1</c>).</summary>
    Connected = 1,

    /// <summary>Actively collecting samples for a run (firmware <c>2</c>).</summary>
    Acquiring = 2,
}
