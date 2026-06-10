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

    [Fact]
    public void Binds_the_rabbitmq_and_discovery_settings_for_status_consumption()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Management:RabbitMq:Host"] = "rabbitmq",
                ["Management:RabbitMq:Port"] = "5672",
                ["Management:RabbitMq:VirtualHost"] = "race-tracker",
                ["Management:RabbitMq:Username"] = "race",
                ["Management:RabbitMq:Password"] = "race",
                ["Management:Discovery:Queue"] = "rt.management.discovery",
                ["Management:Discovery:BindingKey"] = "#",
                ["Management:Discovery:DeadLetterExchange"] = "rt.management.dlx",
                ["Management:Discovery:DeadLetterQueue"] = "rt.management.dlq",
                ["Management:Discovery:Prefetch"] = "16",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplication(configuration);
        using var provider = services.BuildServiceProvider();

        ManagementOptions options = provider.GetRequiredService<IOptions<ManagementOptions>>().Value;

        options.RabbitMq.Host.ShouldBe("rabbitmq");
        options.RabbitMq.Port.ShouldBe(5672);
        options.RabbitMq.VirtualHost.ShouldBe("race-tracker");
        options.RabbitMq.Username.ShouldBe("race");
        options.RabbitMq.Password.ShouldBe("race");
        options.Discovery.Queue.ShouldBe("rt.management.discovery");
        options.Discovery.BindingKey.ShouldBe("#");
        options.Discovery.DeadLetterExchange.ShouldBe("rt.management.dlx");
        options.Discovery.DeadLetterQueue.ShouldBe("rt.management.dlq");
        options.Discovery.Prefetch.ShouldBe((ushort)16);
    }

    [Fact]
    public void Binds_the_mqtt_settings_for_command_dispatch()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Management:Mqtt:Host"] = "mosquitto",
                ["Management:Mqtt:Port"] = "1883",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplication(configuration);
        using var provider = services.BuildServiceProvider();

        MqttOptions mqtt = provider.GetRequiredService<IOptions<ManagementOptions>>().Value.Mqtt;

        mqtt.Host.ShouldBe("mosquitto");
        mqtt.Port.ShouldBe(1883);
    }

    [Fact]
    public void Mqtt_defaults_are_applied_when_unset()
    {
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddApplication(configuration);
        using var provider = services.BuildServiceProvider();

        MqttOptions mqtt = provider.GetRequiredService<IOptions<ManagementOptions>>().Value.Mqtt;

        mqtt.Host.ShouldBe("localhost");
        mqtt.Port.ShouldBe(1883);
    }

    [Fact]
    public void Binds_the_image_size_cap_from_configuration()
    {
        // Only the scalar cap is meant to be overridden in config; the allowed-types array stays the
        // code default (the configuration binder *appends* to a non-empty array default rather than
        // replacing it, so it is intentionally not surfaced in appsettings).
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Management:Images:MaxBytes"] = "1048576",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplication(configuration);
        using var provider = services.BuildServiceProvider();

        ImageOptions images = provider.GetRequiredService<IOptions<ManagementOptions>>().Value.Images;

        images.MaxBytes.ShouldBe(1_048_576);
        images.AllowedContentTypes.ShouldBe(["image/png", "image/jpeg", "image/webp", "image/gif"]);
    }

    [Fact]
    public void Image_defaults_are_applied_when_unset()
    {
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddApplication(configuration);
        using var provider = services.BuildServiceProvider();

        ImageOptions images = provider.GetRequiredService<IOptions<ManagementOptions>>().Value.Images;

        images.MaxBytes.ShouldBe(5L * 1024 * 1024);
        images.AllowedContentTypes.ShouldBe(["image/png", "image/jpeg", "image/webp", "image/gif"]);
    }

    [Fact]
    public void Discovery_defaults_are_applied_when_unset()
    {
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddApplication(configuration);
        using var provider = services.BuildServiceProvider();

        DiscoveryOptions discovery =
            provider.GetRequiredService<IOptions<ManagementOptions>>().Value.Discovery;

        discovery.Queue.ShouldBe("rt.management.discovery");
        discovery.BindingKey.ShouldBe("#");
        discovery.DeadLetterExchange.ShouldBe("rt.management.dlx");
        discovery.DeadLetterQueue.ShouldBe("rt.management.dlq");
        discovery.Prefetch.ShouldBe((ushort)32);
    }
}
