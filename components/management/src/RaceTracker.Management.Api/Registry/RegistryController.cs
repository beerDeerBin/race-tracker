using Microsoft.AspNetCore.Mvc;
using RaceTracker.Management.Application.Crud;
using RaceTracker.Management.Domain.Vehicles;

namespace RaceTracker.Management.Api.Registry;

/// <summary>
/// The registry (<c>/F23/</c>): a read-only lookup other services query to learn which device GUIDs
/// are known and which vehicle they belong to. Backed by the same vehicle store as the CRUD surface
/// (<see cref="CrudService{T}"/>), it returns a slim projection (no metadata). Protected by the
/// fallback authorization policy like every other endpoint; service-key auth (<c>/F13/</c>) is out of
/// scope here.
/// </summary>
[ApiController]
[Route("registry")]
public sealed class RegistryController : ControllerBase
{
    private readonly CrudService<Vehicle> _vehicles;

    public RegistryController(CrudService<Vehicle> vehicles) => _vehicles = vehicles;

    /// <summary>Lists every known GUID and the vehicle it maps to.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RegistryEntryResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Vehicle> vehicles = await _vehicles.GetAllAsync(cancellationToken);
        return Ok(vehicles.Select(ToEntry).ToList());
    }

    /// <summary>Resolves a single GUID to its vehicle, or 404 when the GUID is unknown.</summary>
    [HttpGet("{deviceGuid}")]
    public async Task<ActionResult<RegistryEntryResponse>> GetByGuid(
        string deviceGuid, CancellationToken cancellationToken)
    {
        Vehicle? vehicle = await _vehicles.GetByIdAsync(deviceGuid, cancellationToken);
        return vehicle is null ? NotFound() : Ok(ToEntry(vehicle));
    }

    private static RegistryEntryResponse ToEntry(Vehicle vehicle) =>
        new(vehicle.Id, vehicle.Name, vehicle.Owner, vehicle.RegistrationStatus);
}

/// <summary>A registry entry: a known GUID and the vehicle it belongs to (<c>/F23/</c>).</summary>
public sealed record RegistryEntryResponse(
    string DeviceGuid, string Name, string Owner, RegistrationStatus RegistrationStatus);
