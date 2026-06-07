using RaceTracker.Management.Application.Abstractions;

namespace RaceTracker.Management.Infrastructure.Auth;

/// <summary>
/// Real password hashing adapter (anti-stub) using BCrypt — a strong adaptive hash with a built-in
/// per-password salt and work factor (§8 Sicherheit). The plaintext is never stored.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
