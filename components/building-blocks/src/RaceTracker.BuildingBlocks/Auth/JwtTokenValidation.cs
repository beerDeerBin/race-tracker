using System.Text;
using Microsoft.IdentityModel.Tokens;
using RaceTracker.BuildingBlocks.Contracts.Auth;

namespace RaceTracker.BuildingBlocks.Auth;

/// <summary>
/// Builds the bearer-token validation parameters that mirror what the management issuer
/// signs: the same symmetric key + HMAC-SHA256, issuer/audience/lifetime validation, and the
/// short name/role claim types. Centralised here (story 7.2) so every validating service and
/// the issuer never drift; consumed with <c>MapInboundClaims = false</c>.
/// </summary>
public static class JwtTokenValidation
{
    public static TokenValidationParameters Build(JwtValidationOptions options) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = JwtClaimNames.Name,
        RoleClaimType = JwtClaimNames.Role,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
}
