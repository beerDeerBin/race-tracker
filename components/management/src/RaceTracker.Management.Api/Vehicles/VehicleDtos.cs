using System.ComponentModel.DataAnnotations;
using RaceTracker.Management.Domain.Vehicles;

namespace RaceTracker.Management.Api.Vehicles;

/// <summary>
/// Create-vehicle request (API boundary DTO, kept separate from the <see cref="Vehicle"/> entity).
/// <see cref="DeviceGuid"/> is the verbatim, case-sensitive device GUID and the vehicle's identity.
/// The declarative <c>[Required]</c> annotations give automatic 400 ProblemDetails via <c>[ApiController]</c>.
/// </summary>
public sealed class CreateVehicleRequest
{
    /// <summary>The verbatim device GUID — stored exactly as sent (never normalised).</summary>
    [Required]
    public string DeviceGuid { get; init; } = "";

    /// <summary>Display name.</summary>
    [Required]
    public string Name { get; init; } = "";

    /// <summary>Owner reference; defaults to the authenticated user when omitted.</summary>
    public string? Owner { get; init; }

    /// <summary>Registration status; a manual create defaults to <see cref="RegistrationStatus.Registered"/>.</summary>
    public RegistrationStatus? Status { get; init; }

    /// <summary>Optional free-form metadata.</summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>Update-vehicle request; the GUID identity is immutable and not part of the payload.</summary>
public sealed class UpdateVehicleRequest
{
    /// <summary>Display name.</summary>
    [Required]
    public string Name { get; init; } = "";

    /// <summary>Owner reference; unchanged when omitted.</summary>
    public string? Owner { get; init; }

    /// <summary>Registration status; unchanged when omitted.</summary>
    public RegistrationStatus? Status { get; init; }

    /// <summary>Free-form metadata; unchanged when omitted.</summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>The vehicle as returned to clients (<c>/D20/</c>).</summary>
public sealed record VehicleResponse(
    string DeviceGuid,
    string Name,
    string Owner,
    RegistrationStatus RegistrationStatus,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string> Metadata);
