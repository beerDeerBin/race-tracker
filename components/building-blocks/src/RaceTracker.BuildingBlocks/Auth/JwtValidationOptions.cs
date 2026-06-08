namespace RaceTracker.BuildingBlocks.Auth;

/// <summary>
/// Strongly-typed bearer-validation configuration (<c>/A40/</c>) for every service that
/// validates the management-issued JWTs. Bound from the <see cref="Section"/> section; the
/// <see cref="SigningKey"/> carries a dev default in each service's <c>appsettings</c> and is
/// overridden in real deployments via <c>Auth__Jwt__SigningKey</c> — it must match the
/// issuer's key.
/// </summary>
public sealed class JwtValidationOptions
{
    public const string Section = "Auth:Jwt";

    public string SigningKey { get; init; } = "";
    public string Issuer { get; init; } = "race-tracker-management";
    public string Audience { get; init; } = "race-tracker";
}
