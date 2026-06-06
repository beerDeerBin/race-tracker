using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using DomainTelemetry = RaceTracker.Gateway.Domain.Telemetry;

namespace RaceTracker.Gateway.Application.Messaging;

/// <summary>
/// Maps decoded domain telemetry to the shared <c>/S50/</c> contracts, attaching the device
/// <c>guid</c> (lifted from the MQTT topic by the handler) and the gateway observation time.
/// Keeps the contract types free of any dependency on the gateway's domain (DTOs separate from
/// entities, ARCH §6) and keeps the publisher adapter a pure transport.
/// </summary>
public static class TelemetryContractMapper
{
    /// <summary>Builds a <see cref="StatusEvent"/> from a decoded <see cref="DomainTelemetry.DeviceStatus"/>.</summary>
    public static StatusEvent ToStatusEvent(
        string deviceGuid, DomainTelemetry.DeviceStatus status, DateTimeOffset observedAtUtc) =>
        new(
            deviceGuid,
            status.UptimeMs,
            status.BatteryMv,
            status.BatteryPct,
            ToContractState(status.State),
            status.SampledCount,
            status.TotalSamples,
            status.ErrorCode,
            observedAtUtc);

    /// <summary>Builds a <see cref="SampleBatchEvent"/> from a decoded <see cref="DomainTelemetry.SampleBatch"/>.</summary>
    public static SampleBatchEvent ToSampleBatchEvent(
        string deviceGuid, DomainTelemetry.SampleBatch batch, DateTimeOffset observedAtUtc)
    {
        var samples = new List<ImuSampleDto>(batch.Samples.Count);
        foreach (DomainTelemetry.ImuSample s in batch.Samples)
        {
            samples.Add(new ImuSampleDto(s.Ax, s.Ay, s.Az, s.Gx, s.Gy, s.Gz));
        }

        return new SampleBatchEvent(
            deviceGuid, batch.RunId, batch.StartOffset, batch.Count, samples, observedAtUtc);
    }

    private static DeviceState ToContractState(DomainTelemetry.DeviceState state) => state switch
    {
        DomainTelemetry.DeviceState.Idle => DeviceState.Idle,
        DomainTelemetry.DeviceState.Connected => DeviceState.Connected,
        DomainTelemetry.DeviceState.Acquiring => DeviceState.Acquiring,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown device state."),
    };
}
