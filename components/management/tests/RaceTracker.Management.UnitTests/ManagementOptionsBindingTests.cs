using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RaceTracker.Management.Application;
using RaceTracker.Management.Application.Configuration;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

public sealed class ManagementOptionsBindingTests
{
    [Fact]
    public void Binds_the_management_section_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Management:Mongo:Host"] = "mongodb",
                ["Management:Mongo:Port"] = "27017",
                ["Management:Mongo:Database"] = "racetracker",
                ["Management:Mongo:Username"] = "race",
                ["Management:Mongo:Password"] = "secret",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplication(configuration);
        using var provider = services.BuildServiceProvider();

        ManagementOptions options = provider.GetRequiredService<IOptions<ManagementOptions>>().Value;

        options.Mongo.Host.ShouldBe("mongodb");
        options.Mongo.Port.ShouldBe(27017);
        options.Mongo.Database.ShouldBe("racetracker");
        options.Mongo.Username.ShouldBe("race");
        options.Mongo.Password.ShouldBe("secret");
    }
}
