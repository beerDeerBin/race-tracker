using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RaceTracker.BuildingBlocks.Contracts.Auth;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Domain.Auth;

namespace RaceTracker.Management.Infrastructure.Auth;

/// <summary>
/// Real bearer-token issuer (anti-stub): signs a JWT with the configured symmetric key
/// (HMAC-SHA256) and the standard <c>iss</c>/<c>aud</c>/<c>iat</c>/<c>nbf</c>/<c>exp</c> claims plus
/// the user's <c>sub</c>/<c>name</c>/<c>role</c> (<c>/F11/</c>). Short claim names are used so the
/// token round-trips with <c>MapInboundClaims = false</c> on the validation side. The clock comes
/// from the shared <see cref="TimeProvider"/> so expiry is testable.
/// </summary>
public sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public JwtTokenIssuer(IOptions<AuthOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value.Jwt;
        _timeProvider = timeProvider;
    }

    public AccessToken Issue(User user)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset expires = now.AddMinutes(_options.TokenLifetimeMinutes);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = credentials,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtClaimNames.Sub, user.Id),
                new Claim(JwtClaimNames.Name, user.Username),
                new Claim(JwtClaimNames.Role, user.Role),
                new Claim(JwtClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
        };

        string token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expires);
    }
}
