namespace RaceTracker.BuildingBlocks.Contracts.Auth;

/// <summary>
/// Short JWT claim names — a cross-service auth contract (Leitprinzip "einmalig
/// festgezurrt"): the management issuer writes them and every validating service
/// (management itself, realtime since 7.2, persistence from 7.5) reads the same names.
/// Defined once here so issuer and validators never drift.
/// </summary>
public static class JwtClaimNames
{
    public const string Sub = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string Jti = "jti";
}
