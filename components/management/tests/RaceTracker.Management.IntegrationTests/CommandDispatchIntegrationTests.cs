using System.Buffers;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Domain.Commands;
using RaceTracker.Management.Infrastructure.Commands;
using RaceTracker.Management.Infrastructure.Messaging;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.IntegrationTests;

/// <summary>
/// High-fidelity test of command dispatch (story 5.5 AK1, <c>/F33/</c>) against a real Mosquitto
/// broker in a throwaway container — no mocks. It drives the real <see cref="BinaryCommandEncoder"/>
/// and <see cref="MqttCommandPublisher"/>, then proves a fresh subscriber receives the command on
/// <c>rt/&lt;guid&gt;/cmd</c> with the <b>exact</b> bytes, <b>retained</b>, at <b>QoS 1</b>. (The
/// full REST→…→Timescale E2E with the simulator is the manual verification step / AK2.) Requires Docker.
/// </summary>
public sealed class CommandDispatchIntegrationTests : IAsyncLifetime
{
    private const string MosquittoConf = "listener 1883\nallow_anonymous true\n";

    private readonly IContainer _mosquitto = new ContainerBuilder("eclipse-mosquitto:2")
        .WithResourceMapping(Encoding.UTF8.GetBytes(MosquittoConf), "/mosquitto/config/mosquitto.conf")
        .WithPortBinding(1883, assignRandomHostPort: true)
        .Build();

    private string _host = "";
    private int _port;

    public async Task InitializeAsync()
    {
        await _mosquitto.StartAsync();
        _host = _mosquitto.Hostname;
        _port = _mosquitto.GetMappedPublicPort(1883);
    }

    public Task DisposeAsync() => _mosquitto.DisposeAsync().AsTask();

    [Fact]
    public async Task Start_run_is_published_byte_exact_retained_at_qos1()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        // A mixed-case GUID: the topic must use it verbatim.
        const string guid = "AbCdEf12-3456-7890-ABCD-1234567890Ef";
        const string runId = "12345678-9abc-def0-1122-334455667788";

        byte[] expected = new BinaryCommandEncoder().EncodeStartRun(
            new StartRunCommand(runId, NumSamples: 8330, ImuOdr.Hz104, AccelRange.G4, GyroRange.Dps500));

        MqttApplicationMessage received = await PublishThenReceiveRetainedAsync(guid, expected, cts.Token);

        received.Payload.ToArray().ShouldBe(expected);
        received.Retain.ShouldBeTrue();
        received.QualityOfServiceLevel.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
    }

    [Fact]
    public async Task Connect_is_published_as_the_single_opcode_byte_retained()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        const string guid = "00000000-0000-0000-0000-0000000000c0";

        byte[] expected = new BinaryCommandEncoder().EncodeConnect();

        MqttApplicationMessage received = await PublishThenReceiveRetainedAsync(guid, expected, cts.Token);

        received.Payload.ToArray().ShouldBe([0x01]);
        received.Retain.ShouldBeTrue();
    }

    /// <summary>
    /// Publishes <paramref name="payload"/> via the real publisher, then subscribes — the broker
    /// delivers the retained message with the RETAIN flag set, which is how we assert it was retained.
    /// </summary>
    private async Task<MqttApplicationMessage> PublishThenReceiveRetainedAsync(
        string guid, byte[] payload, CancellationToken cancellationToken)
    {
        var options = Options.Create(new ManagementOptions
        {
            Mqtt = new MqttOptions { Host = _host, Port = _port },
        });

        await using (var publisher = new MqttCommandPublisher(
            options, NullLogger<MqttCommandPublisher>.Instance))
        {
            await publisher.PublishAsync(guid, payload, cancellationToken);
        }

        using IMqttClient subscriber = new MqttClientFactory().CreateMqttClient();
        var received = new TaskCompletionSource<MqttApplicationMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        subscriber.ApplicationMessageReceivedAsync += e =>
        {
            received.TrySetResult(e.ApplicationMessage);
            return Task.CompletedTask;
        };

        MqttClientOptions subOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_host, _port)
            .WithClientId($"it-cmd-sub-{Guid.NewGuid():N}")
            .Build();
        await subscriber.ConnectAsync(subOptions, cancellationToken);
        await subscriber.SubscribeAsync(
            new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(DeviceCommandTopic.For(guid)).WithAtLeastOnceQoS())
                .Build(),
            cancellationToken);

        MqttApplicationMessage message = await received.Task.WaitAsync(
            TimeSpan.FromSeconds(15), cancellationToken);
        await subscriber.DisconnectAsync(cancellationToken: cancellationToken);
        return message;
    }
}
