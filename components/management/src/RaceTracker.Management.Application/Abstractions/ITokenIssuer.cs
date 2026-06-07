using RaceTracker.Management.Domain.Auth;

namespace RaceTracker.Management.Application.Abstractions;

/// <summary>
/// Port that mints a signed bearer token for an authenticated <see cref="User"/> (<c>/F11/</c>).
/// The real adapter signs a JWT (HMAC-SHA256); unit tests mock it.
/// </summary>
public interface ITokenIssuer
{
    AccessToken Issue(User user);
}

/// <summary>A signed bearer token and the instant it expires (UTC).</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
