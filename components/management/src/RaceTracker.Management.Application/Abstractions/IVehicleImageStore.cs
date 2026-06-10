using RaceTracker.Management.Domain.Images;

namespace RaceTracker.Management.Application.Abstractions;

/// <summary>
/// One image read back from the store: its metadata plus an open, readable <see cref="Content"/>
/// stream the caller owns and must dispose. Returned by <see cref="IVehicleImageStore.OpenReadAsync"/>.
/// </summary>
public sealed record VehicleImageContent(VehicleImage Image, Stream Content);

/// <summary>
/// Persistence port (<c>/A30/</c>, anti-stub) for vehicle gallery images: stores the binaries and
/// their metadata keyed by the verbatim, case-sensitive device GUID (CONVENTIONS §8). The real
/// adapter is GridFS (Infrastructure); unit tests mock it. Reads and deletes are always scoped by
/// <c>vehicleGuid</c> <b>and</b> <c>imageId</c> so an image cannot be reached through the wrong
/// vehicle.
/// </summary>
public interface IVehicleImageStore
{
    /// <summary>Stores <paramref name="content"/> for the vehicle and returns its metadata.</summary>
    Task<VehicleImage> UploadAsync(
        string vehicleGuid,
        string fileName,
        string contentType,
        Stream content,
        long length,
        CancellationToken cancellationToken);

    /// <summary>Lists every image stored for the vehicle (newest first), empty when none.</summary>
    Task<IReadOnlyList<VehicleImage>> ListAsync(string vehicleGuid, CancellationToken cancellationToken);

    /// <summary>
    /// Opens the image for reading, or returns <c>null</c> when no such image exists for this vehicle.
    /// The caller owns and disposes <see cref="VehicleImageContent.Content"/>.
    /// </summary>
    Task<VehicleImageContent?> OpenReadAsync(
        string vehicleGuid, string imageId, CancellationToken cancellationToken);

    /// <summary>Returns whether an image with that id exists for this vehicle.</summary>
    Task<bool> ExistsAsync(string vehicleGuid, string imageId, CancellationToken cancellationToken);

    /// <summary>Deletes the image; returns <c>false</c> when it did not exist for this vehicle.</summary>
    Task<bool> DeleteAsync(string vehicleGuid, string imageId, CancellationToken cancellationToken);
}
