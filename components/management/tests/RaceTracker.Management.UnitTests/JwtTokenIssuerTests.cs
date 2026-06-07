using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RaceTracker.BuildingBlocks.Auth;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Domain.Auth;
using RaceTracker.Management.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

/// <summary>
/// Exercises the real JWT issuer (no I/O): the minted token validates against the matching
/// validation parameters and carries the user's name/role; expiry follows the configured lifetime
/// off the injected clock.
/// </summary>
public sealed class JwtTokenIssuerTests
{
    private static readonly AuthOptions _options = new()
    {
        Jwt = new JwtOptions
        {
            SigningKey = "unit-test-signing-key-at-least-32-bytes-long-0123456789",
            Issuer = "race-tracker-management",
            Audience = "race-tracker",
            TokenLifetimeMinutes = 30,
        },
    };

    private static User TestUser() => new()
    {
        Id = "user-1",
        Username = "admin",
        PasswordHash = "irrelevant",
        Role = "admin",
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public async Task Issued_token_validates_and_carries_name_and_role()
    {
        var issuer = new JwtTokenIssuer(Options.Create(_options), TimeProvider.System);

        AccessToken token = issuer.Issue(TestUser());

        TokenValidationResult result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token.Value, JwtTokenValidation.Build(new JwtValidationOptions
            {
                SigningKey = _options.Jwt.SigningKey,
                Issuer = _options.Jwt.Issuer,
                Audience = _options.Jwt.Audience,
            }));

        result.IsValid.ShouldBeTrue();
        result.ClaimsIdentity.Name.ShouldBe("admin");
        result.ClaimsIdentity.FindFirst("role")!.Value.ShouldBe("admin");
        result.ClaimsIdentity.FindFirst("sub")!.Value.ShouldBe("user-1");
    }

    [Fact]
    public void Token_expiry_follows_the_configured_lifetime_and_clock()
    {
        var now = new DateTimeOffset(2026, 6, 6, 12, 0, 0, TimeSpan.Zero);
        var issuer = new JwtTokenIssuer(Options.Create(_options), new FixedTimeProvider(now));

        AccessToken token = issuer.Issue(TestUser());

        token.ExpiresAt.ShouldBe(now.AddMinutes(30));
        // The signed token's own exp matches the returned expiry (to the second).
        JsonWebToken jwt = new JsonWebTokenHandler().ReadJsonWebToken(token.Value);
        jwt.ValidTo.ShouldBe(now.AddMinutes(30).UtcDateTime, TimeSpan.FromSeconds(1));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
