namespace RaceTracker.Management.Application.Images;

/// <summary>
/// Thrown when an uploaded image's content type is not in the configured allowlist. The Api maps it
/// to <c>415 Unsupported Media Type</c>.
/// </summary>
public sealed class UnsupportedImageTypeException(string contentType)
    : Exception($"Image content type '{contentType}' is not allowed.")
{
    /// <summary>The rejected content type.</summary>
    public string ContentType { get; } = contentType;
}

/// <summary>
/// Thrown when an uploaded image exceeds the configured size cap. The Api maps it to
/// <c>413 Payload Too Large</c>.
/// </summary>
public sealed class ImageTooLargeException(long length, long maxBytes)
    : Exception($"Image is {length} bytes, which exceeds the {maxBytes}-byte limit.")
{
    /// <summary>The rejected image's size in bytes.</summary>
    public long Length { get; } = length;

    /// <summary>The configured maximum size in bytes.</summary>
    public long MaxBytes { get; } = maxBytes;
}
