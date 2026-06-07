using Microsoft.AspNetCore.Mvc;
using RaceTracker.Management.Api.Crud;
using RaceTracker.Management.Application.Crud;
using RaceTracker.Management.Domain.Vehicles;

namespace RaceTracker.Management.Api.Vehicles;

/// <summary>
/// REST CRUD for vehicles (<c>/F21/</c>, <c>/F22/</c>, <c>/D20/</c>): the concrete binder of the
/// generic <see cref="CrudControllerBase{TEntity,TCreate,TUpdate,TResponse}"/>. It only supplies the
/// DTO ⇄ entity mapping; the five endpoints come from the base. The device GUID is taken
/// <b>verbatim</b> as the vehicle's identity and is immutable across updates.
/// </summary>
[Route("vehicles")]
public sealed class VehiclesController
    : CrudControllerBase<Vehicle, CreateVehicleRequest, UpdateVehicleRequest, VehicleResponse>
{
    private readonly TimeProvider _timeProvider;

    public VehiclesController(CrudService<Vehicle> service, TimeProvider timeProvider)
        : base(service) => _timeProvider = timeProvider;

    protected override Vehicle ToEntity(CreateVehicleRequest request) => new()
    {
        Id = request.DeviceGuid, // verbatim device GUID — never normalised (case-sensitive correlation key).
        Name = request.Name,
        Owner = string.IsNullOrWhiteSpace(request.Owner) ? (User.Identity?.Name ?? "") : request.Owner,
        RegistrationStatus = request.Status ?? RegistrationStatus.Registered,
        CreatedAt = _timeProvider.GetUtcNow(),
        Metadata = request.Metadata ?? [],
    };

    protected override Vehicle Apply(UpdateVehicleRequest request, Vehicle existing)
    {
        existing.Name = request.Name;
        existing.Owner = request.Owner ?? existing.Owner;
        existing.RegistrationStatus = request.Status ?? existing.RegistrationStatus;
        existing.Metadata = request.Metadata ?? existing.Metadata;
        return existing;
    }

    protected override VehicleResponse ToResponse(Vehicle entity) => new(
        entity.Id, entity.Name, entity.Owner, entity.RegistrationStatus, entity.CreatedAt, entity.Metadata);

    /// <summary>
    /// Claims a discovered vehicle (story 5.4, <c>/F20/</c>): sets its name + owner and flips its
    /// status to <see cref="RegistrationStatus.Registered"/>. Returns 404 when the GUID is unknown.
    /// Reuses the generic single-commit CRUD boundary via <see cref="CrudControllerBase{T,T,T,T}.Service"/>.
    /// </summary>
    [HttpPost("{id}/claim")]
    public async Task<ActionResult<VehicleResponse>> Claim(
        string id, ClaimVehicleRequest request, CancellationToken cancellationToken)
    {
        Vehicle? claimed = await Service.UpdateAsync(id, existing =>
        {
            existing.Name = request.Name;
            existing.Owner = string.IsNullOrWhiteSpace(request.Owner)
                ? (User.Identity?.Name ?? existing.Owner)
                : request.Owner;
            existing.RegistrationStatus = RegistrationStatus.Registered;
            return existing;
        }, cancellationToken);

        return claimed is null ? NotFound() : Ok(ToResponse(claimed));
    }
}
