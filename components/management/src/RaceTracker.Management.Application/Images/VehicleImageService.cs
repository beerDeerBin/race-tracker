using Microsoft.Extensions.Options;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Application.Crud;
using RaceTracker.Management.Domain.Images;
using RaceTracker.Management.Domain.Vehicles;

namespace RaceTracker.Management.Application.Images;

/// <summary>
/// Vehicle gallery use case: validates + stores uploads, lists/reads/deletes images, and keeps the
/// vehicle's <see cref="Vehicle.TitleImageId"/> consistent. It composes the image store
/// (<see cref="IVehicleImageStore"/>) with the shared <see cref="CrudService{T}"/> single-commit
/// boundary (rather than taking a second repository dependency), so a title change is one CRUD
/// update. Validation rules (allowed types + size cap) live <b>here</b>, sourced from
/// <see cref="ImageOptions"/> (<c>/A40/</c>).
/// </summary>
public sealed class VehicleImageService
{
    private readonly IVehicleImageStore _store;
    private readonly CrudService<Vehicle> _vehicles;
    private readonly ImageOptions _options;

    public VehicleImageService(
        IVehicleImageStore store,
        CrudService<Vehicle> vehicles,
        IOptions<ManagementOptions> options)
    {
        _store = store;
        _vehicles = vehicles;
        _options = options.Value.Images;
    }

    /// <summary>
    /// Validates and stores an image for the vehicle. When the vehicle currently has no title image,
    /// the freshly stored one becomes its title (first upload auto-titles).
    /// </summary>
    /// <exception cref="UnsupportedImageTypeException">Content type not in the allowlist.</exception>
    /// <exception cref="ImageTooLargeException">Larger than the configured cap.</exception>
    public async Task<VehicleImage> UploadAsync(
        string vehicleGuid,
        string fileName,
        string contentType,
        Stream content,
        long length,
        CancellationToken cancellationToken)
    {
        if (!IsAllowedType(contentType))
        {
            throw new UnsupportedImageTypeException(contentType);
        }

        if (length > _options.MaxBytes)
        {
            throw new ImageTooLargeException(length, _options.MaxBytes);
        }

        VehicleImage image = await _store.UploadAsync(
            vehicleGuid, fileName, contentType, content, length, cancellationToken);

        // First image becomes the title automatically; never overwrites an existing choice. A missing
        // vehicle simply leaves no title (UpdateAsync is a no-op returning null).
        await _vehicles.UpdateAsync(
            vehicleGuid,
            existing =>
            {
                existing.TitleImageId ??= image.Id;
                return existing;
            },
            cancellationToken);

        return image;
    }

    /// <summary>Lists every image stored for the vehicle.</summary>
    public Task<IReadOnlyList<VehicleImage>> ListAsync(
        string vehicleGuid, CancellationToken cancellationToken) =>
        _store.ListAsync(vehicleGuid, cancellationToken);

    /// <summary>Opens an image for reading, or <c>null</c> when absent for this vehicle.</summary>
    public Task<VehicleImageContent?> OpenReadAsync(
        string vehicleGuid, string imageId, CancellationToken cancellationToken) =>
        _store.OpenReadAsync(vehicleGuid, imageId, cancellationToken);

    /// <summary>
    /// Sets <paramref name="imageId"/> as the vehicle's title image. Returns <c>false</c> when the
    /// image does not exist for this vehicle or the vehicle is unknown (caller answers 404).
    /// </summary>
    public async Task<bool> SetTitleAsync(
        string vehicleGuid, string imageId, CancellationToken cancellationToken)
    {
        if (!await _store.ExistsAsync(vehicleGuid, imageId, cancellationToken))
        {
            return false;
        }

        Vehicle? updated = await _vehicles.UpdateAsync(
            vehicleGuid,
            existing =>
            {
                existing.TitleImageId = imageId;
                return existing;
            },
            cancellationToken);

        return updated is not null;
    }

    /// <summary>
    /// Deletes an image. When the deleted image was the vehicle's title, the title is moved to the
    /// next remaining image (the newest), or cleared to <c>null</c> when none remain — so a vehicle
    /// keeps an avatar as long as it still has any picture. Returns <c>false</c> when the image did
    /// not exist for this vehicle (caller 404s).
    /// </summary>
    public async Task<bool> DeleteAsync(
        string vehicleGuid, string imageId, CancellationToken cancellationToken)
    {
        bool deleted = await _store.DeleteAsync(vehicleGuid, imageId, cancellationToken);
        if (!deleted)
        {
            return false;
        }

        Vehicle? vehicle = await _vehicles.GetByIdAsync(vehicleGuid, cancellationToken);
        if (vehicle is null || vehicle.TitleImageId != imageId)
        {
            // The deleted image was not the title (or the vehicle is gone) — nothing to reassign.
            return true;
        }

        IReadOnlyList<VehicleImage> remaining = await _store.ListAsync(vehicleGuid, cancellationToken);
        string? nextTitle = remaining.Count > 0 ? remaining[0].Id : null;
        await _vehicles.UpdateAsync(
            vehicleGuid,
            existing =>
            {
                existing.TitleImageId = nextTitle;
                return existing;
            },
            cancellationToken);

        return true;
    }

    private bool IsAllowedType(string contentType) =>
        _options.AllowedContentTypes.Any(
            allowed => string.Equals(allowed, contentType, StringComparison.OrdinalIgnoreCase));
}
