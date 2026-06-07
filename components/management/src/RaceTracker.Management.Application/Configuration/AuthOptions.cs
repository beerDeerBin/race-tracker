namespace RaceTracker.Management.Application.Configuration;

/// <summary>
/// Strongly-typed auth configuration (<c>/A40/</c>, <c>/F11/</c>), bound once from the
/// <see cref="Section"/> section and consumed via <c>IOptions&lt;AuthOptions&gt;</c>. Holds the
/// JWT signing/validation parameters and the single seeded user (<c>/O20/</c>).
/// </summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>Bearer-token signing and validation parameters.</summary>
    public JwtOptions Jwt { get; init; } = new();

    /// <summary>The single user seeded at startup in single-user mode (<c>/O20/</c>).</summary>
    public SeedUserOptions SeedUser { get; init; } = new();
}

/// <summary>
/// Symmetric (HMAC-SHA256) JWT parameters. The <see cref="SigningKey"/> is a secret: it carries a
/// dev default in <c>appsettings</c> but is overridden in real deployments via the environment
/// (<c>Auth__Jwt__SigningKey</c>).
/// </summary>
public sealed class JwtOptions
{
    public string SigningKey { get; init; } = "";
    public string Issuer { get; init; } = "race-tracker-management";
    public string Audience { get; init; } = "race-tracker";
    public int TokenLifetimeMinutes { get; init; } = 60;
}

/// <summary>
/// The seeded account. <see cref="Password"/> is the plaintext used <b>only</b> to compute the
/// stored hash at seed time (§8 Sicherheit: never persisted in clear); a dev default lives in
/// <c>appsettings</c> and is overridden via the environment in real deployments.
/// </summary>
public sealed class SeedUserOptions
{
    public string Username { get; init; } = "admin";
    public string Password { get; init; } = "";
    public string Role { get; init; } = "admin";
}
