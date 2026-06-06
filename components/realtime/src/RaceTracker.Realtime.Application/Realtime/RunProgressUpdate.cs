using RaceTracker.BuildingBlocks.Contracts.Telemetry;

namespace RaceTracker.Realtime.Application.Realtime;

/// <summary>
/// The live run-progress push contract (story 6.3, <c>/F61/</c>): the progress counters carried by
/// a <see cref="StatusEvent"/> while the device is <see cref="DeviceState.Acquiring"/>. Pushed to
/// the per-vehicle SignalR group as the <c>"RunProgress"</c> client method (~10 updates per run,
/// one per status keepalive — no polling). Defined once here as the M6 push contract; the
/// frontend's typed model mirrors it in M7.
/// </summary>
/// <param name="DeviceGuid">Device GUID (UUID string) — the cross-service correlation key.</param>
/// <param name="SampledCount">Samples collected so far in the current run.</param>
/// <param name="TotalSamples">Samples requested for the current run.</param>
/// <param name="ObservedAtUtc">When the gateway observed the source status (UTC).</param>
public sealed record RunProgressUpdate(
    string DeviceGuid,
    uint SampledCount,
    uint TotalSamples,
    DateTimeOffset ObservedAtUtc);
