namespace RaceTracker.Gateway.Domain.Telemetry;

/// <summary>
/// A decoded data batch — the typed form of a 24-byte <c>MqttBatchHeader_t</c> followed by
/// <see cref="Count"/> packed sample records (PROTOCOL §6). <see cref="RunId"/> is the
/// canonical UUID string decoded from the header's <c>uint16[8]</c> run id;
/// <see cref="StartOffset"/> is the index of the first sample in the run.
/// </summary>
public sealed record SampleBatch(
    string RunId,
    uint StartOffset,
    uint Count,
    IReadOnlyList<ImuSample> Samples);
