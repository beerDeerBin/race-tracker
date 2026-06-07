using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Gateway.Application.Configuration;
using RaceTracker.Gateway.Infrastructure.Messaging;
using Shouldly;
using Xunit;

namespace RaceTracker.Gateway.IntegrationTests;

/// <summary>
/// High-fidelity test of the real RabbitMQ republish path (AK2/AK3) against a real broker in a
/// throwaway container. Exercises the actual <see cref="RabbitMqTelemetryPublisher"/> adapter —
/// no mocks: it binds a queue to each topic exchange, publishes typed contracts, and asserts the
/// consumed JSON is field-correct and arrives in per-device order (/L30/). Requires Docker.
/// </summary>
public sealed class RabbitMqPublisherIntegrationTests : IAsyncLifetime
{
    private const string DeviceGuid = "00000000-0000-0000-0000-0000000000aa";
    private const string Vhost = "race-tracker";
    private const string User = "race";
    private const string Pass = "race";

    private readonly IContainer _rabbit = new ContainerBuilder("rabbitmq:4-management")
        .WithEnvironment("RABBITMQ_DEFAULT_USER", User)
        .WithEnvironment("RABBITMQ_DEFAULT_PASS", Pass)
        .WithEnvironment("RABBITMQ_DEFAULT_VHOST", Vhost)
        .WithPortBinding(5672, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
        .Build();

    public Task InitializeAsync() => _rabbit.StartAsync();

    public Task DisposeAsync() => _rabbit.DisposeAsync().AsTask();

    [Fact]
    public async Task Publishes_field_correct_contracts_in_order_to_the_topic_exchanges()
    {
        string host = _rabbit.Hostname;
        int port = _rabbit.GetMappedPublicPort(5672);

        var options = Options.Create(new GatewayOptions
        {
            RabbitMq = new RabbitMqOptions
            {
                Host = host,
                Port = port,
                VirtualHost = Vhost,
                Username = User,
                Password = Pass,
            },
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            VirtualHost = Vhost,
            UserName = User,
            Password = Pass,
        };
        await using IConnection connection = await factory.CreateConnectionAsync(cts.Token);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: cts.Token);

        // Declare exchanges + bind one queue per family BEFORE publishing (a topic exchange drops
        // unroutable messages); durable-topic args match the adapter's own declaration.
        await channel.ExchangeDeclareAsync(
            TelemetryExchanges.Status, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cts.Token);
        await channel.ExchangeDeclareAsync(
            TelemetryExchanges.Data, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cts.Token);
        // Exclusive (connection-scoped, auto-removed on close): RabbitMQ 4.x rejects transient
        // non-exclusive queues. Same connection declares, binds and consumes them.
        await channel.QueueDeclareAsync(
            "it-status", durable: false, exclusive: true, autoDelete: true, cancellationToken: cts.Token);
        await channel.QueueDeclareAsync(
            "it-data", durable: false, exclusive: true, autoDelete: true, cancellationToken: cts.Token);
        await channel.QueueBindAsync("it-status", TelemetryExchanges.Status, DeviceGuid, cancellationToken: cts.Token);
        await channel.QueueBindAsync("it-data", TelemetryExchanges.Data, DeviceGuid, cancellationToken: cts.Token);

        var observedAt = DateTimeOffset.UtcNow;
        var status = new StatusEvent(
            DeviceGuid, 1000, 4100, 91, DeviceState.Acquiring, 5, 10, 0x40, observedAt);
        var batch0 = new SampleBatchEvent(
            DeviceGuid, "run-1", 0, 1, [new ImuSampleDto(1, 2, 9.81f, 0, 0, 0)], observedAt);
        var batch32 = new SampleBatchEvent(
            DeviceGuid, "run-1", 32, 1, [new ImuSampleDto(-1, 0, 9.7f, 0, 0, 0)], observedAt);

        await using (var publisher = new RabbitMqTelemetryPublisher(
            options, TimeProvider.System, NullLogger<RabbitMqTelemetryPublisher>.Instance))
        {
            await publisher.PublishStatusAsync(status, cts.Token);
            await publisher.PublishSampleBatchAsync(batch0, cts.Token);
            await publisher.PublishSampleBatchAsync(batch32, cts.Token);
        }

        // Status queue: one field-correct message (AK2).
        StatusEvent receivedStatus = await GetMessageAsync<StatusEvent>(channel, "it-status", cts.Token);
        receivedStatus.DeviceGuid.ShouldBe(DeviceGuid);
        receivedStatus.State.ShouldBe(DeviceState.Acquiring);
        receivedStatus.BatteryMv.ShouldBe((ushort)4100);
        receivedStatus.SampledCount.ShouldBe(5u);
        receivedStatus.ErrorCode.ShouldBe(0x40ul);

        // Data queue: two messages, consumed in publish order (AK3, /L30/).
        SampleBatchEvent first = await GetMessageAsync<SampleBatchEvent>(channel, "it-data", cts.Token);
        SampleBatchEvent second = await GetMessageAsync<SampleBatchEvent>(channel, "it-data", cts.Token);
        first.StartOffset.ShouldBe(0u);
        second.StartOffset.ShouldBe(32u);
        first.RunId.ShouldBe("run-1");
        first.Samples.Count.ShouldBe(1);
        first.Samples[0].Az.ShouldBe(9.81f);
    }

    private static async Task<T> GetMessageAsync<T>(
        IChannel channel, string queue, CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            BasicGetResult? result = await channel.BasicGetAsync(queue, autoAck: true, cancellationToken);
            if (result is not null)
            {
                return JsonSerializer.Deserialize<T>(result.Body.Span)
                    ?? throw new InvalidOperationException("Message body deserialized to null.");
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException($"No message arrived on queue '{queue}'.");
    }
}
