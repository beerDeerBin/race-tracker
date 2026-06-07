namespace RaceTracker.Persistence.Domain.Telemetry;

/// <summary>
/// One persisted sample of a run: its <see cref="Index"/> is the <b>absolute</b> sample index
/// within the run (the batch's start offset plus the reading's position), so a sample is keyed
/// by <c>deviceGuid</c> + <c>runId</c> + <see cref="Index"/> and upserts idempotently (/F54/).
/// There is no timestamp — time is derived as <c>t = index / odr_hz</c> (/F54/, PROTOCOL §6).
/// </summary>
public sealed record RunSample(long Index, ImuReading Reading);
