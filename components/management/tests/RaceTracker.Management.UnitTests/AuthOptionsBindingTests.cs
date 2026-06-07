using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RaceTracker.Management.Application;
using RaceTracker.Management.Application.Configuration;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

/// <summary>
/// Verifies the <c>Auth</c> section binds into <see cref="AuthOptions"/> (Options pattern, /A40/),
/// including the nested JWT and seed-user settings.
/// </summary>
public sealed class AuthOptionsBindingTests
{
    [Fact]
    public void Binds_the_auth_section_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Jwt:SigningKey"] = "binding-test-signing-key-at-least-32-bytes-0123",
                ["Auth:Jwt:Issuer"] = "race-tracker-management",
                ["Auth:Jwt:Audience"] = "race-tracker",
                ["Auth:Jwt:TokenLifetimeMinutes"] = "120",
                ["Auth:SeedUser:Username"] = "racer",
                ["Auth:SeedUser:Password"] = "seed-pw",
                ["Auth:SeedUser:Role"] = "admin",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplication(configuration);
        using var provider = services.BuildServiceProvider();

        AuthOptions options = provider.GetRequiredService<IOptions<AuthOptions>>().Value;

        options.Jwt.SigningKey.ShouldBe("binding-test-signing-key-at-least-32-bytes-0123");
        options.Jwt.Issuer.ShouldBe("race-tracker-management");
        options.Jwt.Audience.ShouldBe("race-tracker");
        options.Jwt.TokenLifetimeMinutes.ShouldBe(120);
        options.SeedUser.Username.ShouldBe("racer");
        options.SeedUser.Password.ShouldBe("seed-pw");
        options.SeedUser.Role.ShouldBe("admin");
    }
}
