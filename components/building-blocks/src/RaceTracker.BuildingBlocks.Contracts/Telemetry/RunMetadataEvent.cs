namespace RaceTracker.BuildingBlocks.Contracts.Telemetry;

/// <summary>
/// Run-metadata announcement (/F31/, /F54/): the parameters of a run, published by the management
/// service to the <see cref="TelemetryExchanges.Run"/> exchange (keyed by <see cref="DeviceGuid"/>)
/// the moment it dispatches a <c>START_RUN</c> command. The device never echoes these back (its
/// outbound status/data carry only counts + telemetry, PROTOCOL §5/§6), so this is the <b>only</b>
/// path by which the persistence service learns a run's <see cref="OdrHz"/> — the time base for the
/// stored samples (<c>t = index / odr_hz</c>). Consumed by persistence to fill the otherwise-NULL
/// run-metadata columns; idempotent on <see cref="DeviceGuid"/> + <see cref="RunId"/>.
/// </summary>
/// <param name="DeviceGuid">Device GUID (UUID string) — the cross-service correlation key, verbatim.</param>
/// <param name="RunId">The caller-chosen run UUID sent in <c>START_RUN</c> (later seen on the samples).</param>
/// <param name="NumSamples">Total samples requested for the run.</param>
/// <param name="OdrHz">Output data rate in Hz — the run's time base (the resolved physical rate).</param>
/// <param name="AccelRange">Accelerometer full-scale range as the wire byte (PROTOCOL §4.2).</param>
/// <param name="GyroRange">Gyroscope full-scale range as the wire byte (PROTOCOL §4.2).</param>
/// <param name="StartedAtUtc">When management dispatched the <c>START_RUN</c> command (UTC).</param>
public sealed record RunMetadataEvent(
    string DeviceGuid,
    string RunId,
    uint NumSamples,
    int OdrHz,
    short AccelRange,
    short GyroRange,
    DateTimeOffset StartedAtUtc);
