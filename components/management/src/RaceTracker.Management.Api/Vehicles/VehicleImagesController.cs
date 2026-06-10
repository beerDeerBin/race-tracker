using Microsoft.AspNetCore.Mvc;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Images;
using RaceTracker.Management.Domain.Images;

namespace RaceTracker.Management.Api.Vehicles;

/// <summary>
/// REST surface for a vehicle's image gallery, nested under the vehicle's verbatim device GUID. The
/// binary upload/download lives here (multipart in, stream out); the metadata round-trips as
/// <see cref="VehicleImageResponse"/>. Carries no <c>[AllowAnonymous]</c>, so the fallback
/// authorization policy protects every action (secure-by-default, <c>/F12/</c>). All work is delegated
/// to <see cref="VehicleImageService"/>, which owns validation + title-image consistency.
/// </summary>
[ApiController]
[Route("vehicles/{deviceGuid}/images")]
public sealed class VehicleImagesController : ControllerBase
{
    private readonly VehicleImageService _images;

    public VehicleImagesController(VehicleImageService images) => _images = images;

    /// <summary>Lists the vehicle's images (newest first).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleImageResponse>>> List(
        string deviceGuid, CancellationToken cancellationToken)
    {
        IReadOnlyList<VehicleImage> images = await _images.ListAsync(deviceGuid, cancellationToken);
        return Ok(images.Select(ToResponse).ToList());
    }

    /// <summary>
    /// Uploads an image (multipart <c>file</c>). Returns 201 with its metadata; 415 when the content
    /// type is not allowed, 413 when it exceeds the configured size cap, 400 when no file was sent.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<VehicleImageResponse>> Upload(
        string deviceGuid, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "No image file was provided.");
        }

        try
        {
            await using Stream content = file.OpenReadStream();
            VehicleImage image = await _images.UploadAsync(
                deviceGuid, file.FileName, file.ContentType, content, file.Length, cancellationToken);

            return CreatedAtAction(
                nameof(Download), new { deviceGuid, imageId = image.Id }, ToResponse(image));
        }
        catch (UnsupportedImageTypeException exception)
        {
            return Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, detail: exception.Message);
        }
        catch (ImageTooLargeException exception)
        {
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: exception.Message);
        }
    }

    /// <summary>Streams the image bytes with its content type; 404 when unknown for this vehicle.</summary>
    [HttpGet("{imageId}")]
    public async Task<IActionResult> Download(
        string deviceGuid, string imageId, CancellationToken cancellationToken)
    {
        VehicleImageContent? content = await _images.OpenReadAsync(deviceGuid, imageId, cancellationToken);
        if (content is null)
        {
            return NotFound();
        }

        // Images are immutable per id; let the browser cache privately (the request carries a token).
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(content.Content, content.Image.ContentType, content.Image.FileName);
    }

    /// <summary>Sets the image as the vehicle's title image; 404 when unknown for this vehicle.</summary>
    [HttpPut("{imageId}/title")]
    public async Task<IActionResult> SetTitle(
        string deviceGuid, string imageId, CancellationToken cancellationToken)
    {
        bool set = await _images.SetTitleAsync(deviceGuid, imageId, cancellationToken);
        return set ? NoContent() : NotFound();
    }

    /// <summary>Deletes the image (clearing the title when it was the current one); 404 when unknown.</summary>
    [HttpDelete("{imageId}")]
    public async Task<IActionResult> Delete(
        string deviceGuid, string imageId, CancellationToken cancellationToken)
    {
        bool deleted = await _images.DeleteAsync(deviceGuid, imageId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private static VehicleImageResponse ToResponse(VehicleImage image) => new(
        image.Id, image.FileName, image.ContentType, image.Length, image.UploadedAt);
}
