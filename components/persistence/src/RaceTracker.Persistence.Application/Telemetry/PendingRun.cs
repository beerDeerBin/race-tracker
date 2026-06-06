namespace RaceTracker.Persistence.Application.Telemetry;

/// <summary>
/// A run whose derived trajectory needs (re)building (story 4.3): its <see cref="DeviceGuid"/> +
/// <see cref="RunId"/> key and the stored <see cref="OdrHz"/> (null until a producer supplies it,
/// in which case the projection falls back to the configured default ODR).
/// </summary>
public sealed record PendingRun(Guid DeviceGuid, Guid RunId, int? OdrHz);
