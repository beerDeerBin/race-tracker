namespace RaceTracker.Management.Application.Abstractions;

/// <summary>
/// Port for the password hashing scheme (§8 Sicherheit: a strong adaptive hash, never plaintext).
/// The real adapter uses BCrypt; unit tests can mock it, but the real hasher is also exercised
/// directly since it has no I/O.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Computes a salted adaptive hash of <paramref name="password"/>.</summary>
    string Hash(string password);

    /// <summary>Verifies <paramref name="password"/> against a stored <paramref name="passwordHash"/>.</summary>
    bool Verify(string password, string passwordHash);
}
