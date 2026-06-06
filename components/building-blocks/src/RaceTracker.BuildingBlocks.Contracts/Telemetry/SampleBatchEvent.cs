namespace RaceTracker.BuildingBlocks.Contracts.Telemetry;

/// <summary>
/// Normalised sample-batch event (/S50/, /D50/): a decoded data batch (PROTOCOL §6) plus the
/// <see cref="DeviceGuid"/> from the MQTT topic. Published by the gateway to the
/// <see cref="TelemetryExchanges.Data"/> exchange with the <see cref="DeviceGuid"/> as the
/// routing key. Telemetry is keyed by <see cref="DeviceGuid"/> + <see cref="RunId"/> + sample
/// index, where a sample's absolute index is <see cref="StartOffset"/> + its position in
/// <see cref="Samples"/> — so batches stay independently upsertable. Defined once in M2.
/// </summary>
/// <param name="DeviceGuid">Device GUID (UUID string) — the cross-service correlation key.</param>
/// <param name="RunId">Run UUID echoed from the batch header (the <c>START_RUN</c> runId).</param>
/// <param name="StartOffset">Absolute index of the first sample in the run.</param>
/// <param name="Count">Number of samples in <see cref="Samples"/> (batch header count).</param>
/// <param name="Samples">The decoded IMU readings, in run order.</param>
/// <param name="ObservedAtUtc">When the gateway observed the message (UTC).</param>
public sealed record SampleBatchEvent(
    string DeviceGuid,
    string RunId,
    uint StartOffset,
    uint Count,
    IReadOnlyList<ImuSampleDto> Samples,
    DateTimeOffset ObservedAtUtc);
