using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RaceTracker.Realtime.Application;
using RaceTracker.Realtime.Application.Configuration;
using Shouldly;
using Xunit;

namespace RaceTracker.Realtime.UnitTests;

public sealed class RealtimeOptionsBindingTests
{
    [Fact]
    public void Binds_the_realtime_section_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Realtime:RabbitMq:Host"] = "rabbitmq",
                ["Realtime:RabbitMq:Port"] = "5672",
                ["Realtime:RabbitMq:VirtualHost"] = "race-tracker",
                ["Realtime:RabbitMq:Username"] = "race",
                ["Realtime:RabbitMq:Password"] = "race",
                ["Realtime:Relay:BindingKey"] = "#",
                ["Realtime:Relay:Prefetch"] = "64",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplication(configuration);
        using var provider = services.BuildServiceProvider();

        RealtimeOptions options = provider.GetRequiredService<IOptions<RealtimeOptions>>().Value;

        options.RabbitMq.Host.ShouldBe("rabbitmq");
        options.RabbitMq.Port.ShouldBe(5672);
        options.RabbitMq.VirtualHost.ShouldBe("race-tracker");
        options.RabbitMq.Username.ShouldBe("race");
        options.RabbitMq.Password.ShouldBe("race");
        options.Relay.BindingKey.ShouldBe("#");
        options.Relay.Prefetch.ShouldBe((ushort)64);
    }
}
