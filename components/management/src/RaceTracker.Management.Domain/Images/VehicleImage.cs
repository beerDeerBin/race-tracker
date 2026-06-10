namespace RaceTracker.Management.Domain.Images;

/// <summary>
/// Metadata for one image in a vehicle's gallery — the binary itself lives in the image store
/// (GridFS), this is the typed view of it. Plain POCO with zero framework dependencies; the storage
/// mapping lives in Infrastructure. <see cref="VehicleGuid"/> is the verbatim, case-sensitive device
/// GUID (CONVENTIONS §8) the image belongs to, kept as an opaque string and never re-cased.
/// </summary>
public sealed class VehicleImage
{
    /// <summary>Stable storage identity of the image (opaque string).</summary>
    public required string Id { get; init; }

    /// <summary>The verbatim device GUID this image belongs to.</summary>
    public required string VehicleGuid { get; init; }

    /// <summary>Original file name as uploaded (display only).</summary>
    public required string FileName { get; init; }

    /// <summary>MIME type of the stored binary (e.g. <c>image/png</c>).</summary>
    public required string ContentType { get; init; }

    /// <summary>Size of the stored binary in bytes.</summary>
    public required long Length { get; init; }

    /// <summary>When the image was stored (UTC).</summary>
    public required DateTimeOffset UploadedAt { get; init; }
}
