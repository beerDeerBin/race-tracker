using RaceTracker.Management.Domain.Abstractions;

namespace RaceTracker.Management.Domain.Vehicles;

/// <summary>
/// A tracked vehicle (<c>/D20/</c>, <c>/F21/</c>) — the first CRUD entity of the management service.
/// Its identity <see cref="Id"/> <b>is</b> the device GUID, the service-spanning correlation key
/// (Leitprinzip <i>einmalig festgezurrt</i>, CONVENTIONS §8). The GUID is an <b>opaque,
/// case-sensitive string</b> and is stored <b>verbatim</b> — never round-tripped through
/// <see cref="System.Guid"/> (whose <c>ToString()</c> lower-cases), because it must keep matching
/// the case-sensitive MQTT command topic <c>rt/&lt;guid&gt;/cmd</c> used later (story 5.5).
/// Plain POCO with zero framework dependencies; the Mongo mapping lives in Infrastructure.
/// </summary>
public sealed class Vehicle : IEntity
{
    /// <summary>Stable identity = the verbatim device GUID (maps to Mongo's <c>_id</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Human-friendly display name (<c>Anzeigename</c>).</summary>
    public required string Name { get; set; }

    /// <summary>Owner reference (the single seeded user today; the field stays for multi-user).</summary>
    public required string Owner { get; set; }

    /// <summary>Registration lifecycle (<c>pending</c>/<c>registered</c>).</summary>
    public required RegistrationStatus RegistrationStatus { get; set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Free-form metadata (<c>/D20/</c>); empty when none was supplied.</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
