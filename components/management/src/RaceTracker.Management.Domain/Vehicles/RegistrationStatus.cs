namespace RaceTracker.Management.Domain.Vehicles;

/// <summary>
/// Registration lifecycle of a <see cref="Vehicle"/> (<c>/D20/</c>, <c>/F21/</c>). A device that
/// turns up on its own (lazy discovery, story 5.4) starts <see cref="Pending"/>; a user-created or
/// claimed vehicle is <see cref="Registered"/>. Persisted as its string name (BSON class map) so
/// the stored value stays readable and stable if the enum gains members.
/// </summary>
public enum RegistrationStatus
{
    /// <summary>Unknown/unclaimed device that surfaced from telemetry but has no owner yet.</summary>
    Pending,

    /// <summary>A named, owned vehicle (manually created or claimed).</summary>
    Registered,
}
