using Microsoft.Extensions.Logging;
using RaceTracker.Management.Application.Crud;
using RaceTracker.Management.Application.Observability;
using RaceTracker.Management.Domain.Vehicles;

namespace RaceTracker.Management.Application.Discovery;

/// <summary>
/// Device-discovery use case (<c>/F20/</c>, <c>/F24/</c>, <c>/F25/</c>, <c>/O50/</c>): when a status
/// event arrives for a GUID the management store has never seen, lazily register it as a
/// <see cref="RegistrationStatus.Pending"/> vehicle so the device surfaces in the dashboard and no
/// data is lost. The actual write reuses the generic <see cref="CrudService{T}"/> (anti-refactor:
/// the M5 CRUD building block, not a second persistence path), whose <c>CreateAsync</c> skips ids
/// that already exist without writing — so a device that is already <c>pending</c> <b>or</b> already
/// claimed (<c>registered</c>) is left untouched (a late status event never downgrades a claimed
/// vehicle). Scoped, because <see cref="CrudService{T}"/> and its Mongo ports are scoped.
/// </summary>
public sealed partial class DeviceDiscoveryService
{
    private readonly CrudService<Vehicle> _vehicles;
    private readonly ManagementMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeviceDiscoveryService> _logger;

    public DeviceDiscoveryService(
        CrudService<Vehicle> vehicles, ManagementMetrics metrics, TimeProvider timeProvider,
        ILogger<DeviceDiscoveryService> logger)
    {
        _vehicles = vehicles;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Ensures a vehicle exists for <paramref name="deviceGuid"/>, creating a <c>pending</c> one when
    /// the GUID is unknown. Returns <c>true</c> when a new pending vehicle was created, <c>false</c>
    /// when the GUID was already known. The GUID is stored <b>verbatim</b> (the case-sensitive
    /// cross-service correlation key) — never round-tripped through <see cref="System.Guid"/>.
    /// </summary>
    public async Task<bool> EnsurePendingAsync(string deviceGuid, CancellationToken cancellationToken)
    {
        var pending = new Vehicle
        {
            Id = deviceGuid, // verbatim device GUID — never normalised (case-sensitive correlation key).
            Name = deviceGuid, // a sensible default display name until the user claims + renames it.
            Owner = "", // unowned until claimed.
            RegistrationStatus = RegistrationStatus.Pending,
            CreatedAt = _timeProvider.GetUtcNow(),
            Metadata = [],
        };

        bool created = await _vehicles.CreateAsync(pending, cancellationToken);
        if (created)
        {
            _metrics.RecordDiscovered();
            LogDiscovered(deviceGuid);
        }

        return created;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Discovered unknown device {DeviceGuid}; registered a pending vehicle")]
    private partial void LogDiscovered(string deviceGuid);
}
