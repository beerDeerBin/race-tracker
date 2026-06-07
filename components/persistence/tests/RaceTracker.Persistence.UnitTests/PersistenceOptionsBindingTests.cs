using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RaceTracker.Persistence.Application;
using RaceTracker.Persistence.Application.Configuration;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

public sealed class PersistenceOptionsBindingTests
{
    [Fact]
    public void Binds_the_persistence_section_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:RabbitMq:Host"] = "rabbitmq",
                ["Persistence:RabbitMq:Port"] = "5672",
                ["Persistence:RabbitMq:VirtualHost"] = "race-tracker",
                ["Persistence:RabbitMq:Username"] = "race",
                ["Persistence:RabbitMq:Password"] = "race",
                ["Persistence:Timescale:Host"] = "timescaledb",
                ["Persistence:Timescale:Port"] = "5432",
                ["Persistence:Timescale:Database"] = "racetracker",
                ["Persistence:Timescale:Username"] = "race",
                ["Persistence:Timescale:Password"] = "race",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplication(configuration);
        using var provider = services.BuildServiceProvider();

        PersistenceOptions options = provider.GetRequiredService<IOptions<PersistenceOptions>>().Value;

        options.RabbitMq.Host.ShouldBe("rabbitmq");
        options.RabbitMq.Port.ShouldBe(5672);
        options.RabbitMq.VirtualHost.ShouldBe("race-tracker");
        options.RabbitMq.Username.ShouldBe("race");
        options.RabbitMq.Password.ShouldBe("race");
        options.Timescale.Host.ShouldBe("timescaledb");
        options.Timescale.Port.ShouldBe(5432);
        options.Timescale.Database.ShouldBe("racetracker");
        options.Timescale.Username.ShouldBe("race");
        options.Timescale.Password.ShouldBe("race");
    }
}
