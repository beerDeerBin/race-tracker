namespace RaceTracker.Management.Domain.Auth;

/// <summary>
/// The management user (<c>/D10/</c>). In the single-user mode (<c>/O20/</c>) exactly one
/// record is seeded; the <see cref="Role"/> stays so multi-user is retrofittable without a
/// migration. The password is held only as a <see cref="PasswordHash"/> — the plaintext is
/// never stored (§8 Sicherheit). Plain POCO with zero framework dependencies.
/// </summary>
public sealed class User
{
    /// <summary>Stable identifier (maps to Mongo's <c>_id</c>); a UUID string.</summary>
    public required string Id { get; init; }

    /// <summary>Login name the user authenticates with (unique).</summary>
    public required string Username { get; init; }

    /// <summary>Adaptive password hash (BCrypt); never the plaintext.</summary>
    public required string PasswordHash { get; init; }

    /// <summary>Authorization role, e.g. <c>admin</c> for the seeded single user.</summary>
    public required string Role { get; init; }

    /// <summary>Creation timestamp (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
