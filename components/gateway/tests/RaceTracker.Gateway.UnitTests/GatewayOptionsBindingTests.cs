using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RaceTracker.Gateway.Application;
using RaceTracker.Gateway.Application.Configuration;
using Shouldly;
using Xunit;

namespace RaceTracker.Gateway.UnitTests;

public sealed class GatewayOptionsBindingTests
{
    [Fact]
    public void Binds_the_gateway_section_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:Mqtt:Host"] = "mosquitto",
                ["Gateway:Mqtt:Port"] = "1883",
                ["Gateway:RabbitMq:Host"] = "rabbitmq",
                ["Gateway:RabbitMq:Port"] = "5672",
                ["Gateway:RabbitMq:VirtualHost"] = "race-tracker",
                ["Gateway:RabbitMq:Username"] = "race",
                ["Gateway:RabbitMq:Password"] = "race",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplication(configuration);
        using var provider = services.BuildServiceProvider();

        GatewayOptions options = provider.GetRequiredService<IOptions<GatewayOptions>>().Value;

        options.Mqtt.Host.ShouldBe("mosquitto");
        options.Mqtt.Port.ShouldBe(1883);
        options.RabbitMq.Host.ShouldBe("rabbitmq");
        options.RabbitMq.Port.ShouldBe(5672);
        options.RabbitMq.VirtualHost.ShouldBe("race-tracker");
        options.RabbitMq.Username.ShouldBe("race");
        options.RabbitMq.Password.ShouldBe("race");
    }
}
