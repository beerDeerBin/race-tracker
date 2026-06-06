using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MQTTnet;
using RaceTracker.Gateway.Application.Configuration;
using RaceTracker.Gateway.Application.Ingestion;
using RaceTracker.Gateway.Application.Observability;
using RaceTracker.Gateway.Domain.Telemetry;
using RaceTracker.Gateway.Infrastructure.Decoding;
using RaceTracker.Gateway.Infrastructure.Mqtt;
using RaceTracker.Gateway.UnitTests.Support;
using Shouldly;
using Xunit;

namespace RaceTracker.Gateway.IntegrationTests;

/// <summary>
/// High-fidelity test of the live subscribe→decode→metric path (AK1) against a real
/// Mosquitto broker in a throwaway container (CONVENTIONS §7). Exercises the real MQTTnet
/// subscriber adapter and binary decoder end to end — no mocks. Requires Docker.
/// </summary>
public sealed class MqttIngestionIntegrationTests : IAsyncLifetime
{
    private const string DeviceGuid = "00000000-0000-0000-0000-000000000099";
    private const string MosquittoConf = "listener 1883\nallow_anonymous true\n";

    private readonly IContainer _mosquitto = new ContainerBuilder("eclipse-mosquitto:2")
        .WithResourceMapping(Encoding.UTF8.GetBytes(MosquittoConf), "/mosquitto/config/mosquitto.conf")
        .WithPortBinding(1883, assignRandomHostPort: true)
        .Build();

    public Task InitializeAsync() => _mosquitto.StartAsync();

    public Task DisposeAsync() => _mosquitto.DisposeAsync().AsTask();

    [Fact]
    public async Task Subscribes_decodes_and_counts_real_broker_traffic()
    {
        string host = _mosquitto.Hostname;
        int port = _mosquitto.GetMappedPublicPort(1883);

        var options = Options.Create(new GatewayOptions
        {
            Mqtt = new MqttOptions { Host = host, Port = port },
        });
        using var metrics = new GatewayMetrics();
        using var collector = new MetricsCollector(GatewayMetrics.MeterName);
        var handler = new TelemetryMessageHandler(
            new BinaryDecoder(), metrics, NullLogger<TelemetryMessageHandler>.Instance);
        var subscriber = new MqttTelemetrySubscriber(
            options, NullLogger<MqttTelemetrySubscriber>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await subscriber.StartAsync(handler.HandleAsync, cts.Token);
            // Let the ConnectedAsync handler establish the QoS-0 subscriptions before publishing.
            await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

            byte[] runId = new byte[16];
            runId[14] = 0x01;

            await PublishAsync(host, port, $"rt/{DeviceGuid}/status",
                PayloadFactory.Status(1000, 4100, 91, state: 1, 0, 0, 0), cts.Token);
            await PublishAsync(host, port, $"rt/{DeviceGuid}/data",
                PayloadFactory.DataBatch(runId, startOffset: 0,
                    samples: [new ImuSample(1, 2, 9.81f, 0, 0, 0), new ImuSample(-1, 0, 9.7f, 0, 0, 0)]),
                cts.Token);
            // Malformed status payload (wrong length) — must be dropped + counted, not decoded.
            await PublishAsync(host, port, $"rt/{DeviceGuid}/status", new byte[5], cts.Token);

            await WaitUntilAsync(
                () => collector.Total("gateway.messages.received") >= 3,
                TimeSpan.FromSeconds(15));

            collector.Total("gateway.messages.received").ShouldBe(3);
            collector.Total("gateway.messages.decoded").ShouldBe(2);
            collector.Total("gateway.messages.failed").ShouldBe(1);
        }
        finally
        {
            await subscriber.StopAsync(CancellationToken.None);
            await subscriber.DisposeAsync();
        }
    }

    private static async Task PublishAsync(
        string host, int port, string topic, byte[] payload, CancellationToken cancellationToken)
    {
        using IMqttClient client = new MqttClientFactory().CreateMqttClient();
        MqttClientOptions options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId($"it-pub-{Guid.NewGuid():N}")
            .Build();

        await client.ConnectAsync(options, cancellationToken);
        MqttApplicationMessage message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .Build();
        await client.PublishAsync(message, cancellationToken);
        await client.DisconnectAsync(cancellationToken: cancellationToken);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(100);
        }
    }
}
