namespace RaceTracker.Management.Domain.Commands;

/// <summary>
/// The parameters of a <c>START_RUN</c> command (PROTOCOL.md §4.2, <c>/F31/</c>, <c>/D40/</c>): the
/// caller-chosen <paramref name="RunId"/> (UUID string — the cross-service correlation key, echoed
/// back by the device and later seen on the run's samples) plus the sample count and IMU ranges. A
/// plain value object with zero framework dependencies; the byte encoding lives in the encoder
/// adapter and the request validation at the API boundary.
/// </summary>
/// <param name="RunId">Caller-chosen run UUID (encoded via the shared <c>RunIdCodec</c>).</param>
/// <param name="NumSamples">Total samples to collect in the run.</param>
/// <param name="Odr">Output data rate (also the run's time base).</param>
/// <param name="AccelRange">Accelerometer full-scale range.</param>
/// <param name="GyroRange">Gyroscope full-scale range.</param>
public sealed record StartRunCommand(
    string RunId,
    uint NumSamples,
    ImuOdr Odr,
    AccelRange AccelRange,
    GyroRange GyroRange);
