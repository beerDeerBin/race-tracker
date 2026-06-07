namespace RaceTracker.Management.Application.Auth;

/// <summary>
/// Short JWT claim names shared by the token issuer, the validation parameters and the protected
/// endpoints that read the principal. Kept in the Application layer so the issuing/validation
/// adapters (Infrastructure) and the auth endpoints (Api) reference the same names without the Api
/// reaching into Infrastructure.
/// </summary>
public static class JwtClaimNames
{
    public const string Sub = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string Jti = "jti";
}
