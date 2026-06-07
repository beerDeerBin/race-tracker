using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Commands;
using RaceTracker.Management.Application.Observability;
using RaceTracker.Management.Domain.Commands;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

/// <summary>
/// Unit tests for the command-dispatch use case (story 5.5) with its encoder + publisher ports mocked.
/// Proves each method publishes the encoder's exact bytes to the <b>verbatim</b> device GUID, counts a
/// <c>sent</c> command, and that a transport failure is counted as <c>failed</c> and rethrown. Also
/// covers the START_RUN run-metadata announcement (/F54/): the resolved ODR/ranges are published, and
/// a failed announcement is swallowed (best-effort) so the run is not failed.
/// </summary>
public sealed class CommandServiceTests : IDisposable
{
    // A deliberately MIXED-case GUID: the topic/guid must be passed through unchanged.
    private const string MixedCaseGuid = "AbCdEf12-3456-7890-ABCD-1234567890Ef";

    private readonly ICommandEncoder _encoder = Substitute.For<ICommandEncoder>();
    private readonly ICommandPublisher _publisher = Substitute.For<ICommandPublisher>();
    private readonly IRunMetadataPublisher _runMetadataPublisher = Substitute.For<IRunMetadataPublisher>();
    private readonly ManagementMetrics _metrics = new();

    public void Dispose() => _metrics.Dispose();

    private CommandService CreateSut() =>
        new(_encoder, _publisher, _runMetadataPublisher, TimeProvider.System, _metrics,
            NullLogger<CommandService>.Instance);

    [Fact]
    public async Task Connect_publishes_the_encoded_bytes_to_the_verbatim_guid()
    {
        byte[] bytes = [0x01];
        _encoder.EncodeConnect().Returns(bytes);

        using var commands = new CommandCounter();
        await CreateSut().ConnectAsync(MixedCaseGuid, CancellationToken.None);

        await _publisher.Received(1).PublishAsync(
            MixedCaseGuid,
            Arg.Is<ReadOnlyMemory<byte>>(m => m.ToArray().SequenceEqual(bytes)),
            Arg.Any<CancellationToken>());
        commands.Count("connect", "sent").ShouldBe(1);
    }

    [Fact]
    public async Task Start_run_publishes_the_encoded_start_run_bytes()
    {
        var command = new StartRunCommand(
            "00000000-0000-0000-0000-000000000001", 100, ImuOdr.Hz104, AccelRange.G4, GyroRange.Dps500);
        byte[] bytes = [0x02, 0xAB, 0xCD];
        _encoder.EncodeStartRun(command).Returns(bytes);

        using var commands = new CommandCounter();
        await CreateSut().StartRunAsync(MixedCaseGuid, command, CancellationToken.None);

        await _publisher.Received(1).PublishAsync(
            MixedCaseGuid,
            Arg.Is<ReadOnlyMemory<byte>>(m => m.ToArray().SequenceEqual(bytes)),
            Arg.Any<CancellationToken>());
        commands.Count("start_run", "sent").ShouldBe(1);
    }

    [Fact]
    public async Task Start_run_announces_run_metadata_with_resolved_odr_and_verbatim_guid()
    {
        var command = new StartRunCommand(
            "00000000-0000-0000-0000-000000000001", 8330, ImuOdr.Hz208, AccelRange.G4, GyroRange.Dps500);
        _encoder.EncodeStartRun(command).Returns([0x02]);

        using var commands = new CommandCounter();
        await CreateSut().StartRunAsync(MixedCaseGuid, command, CancellationToken.None);

        // The run's parameters reach persistence verbatim: guid unchanged, runId echoed, ODR resolved
        // to Hz (the time base), ranges as their wire bytes (G4 = 0x02, Dps500 = 0x02).
        await _runMetadataPublisher.Received(1).PublishAsync(
            Arg.Is<RunMetadataEvent>(e =>
                e.DeviceGuid == MixedCaseGuid
                && e.RunId == command.RunId
                && e.NumSamples == 8330
                && e.OdrHz == 208
                && e.AccelRange == 0x02
                && e.GyroRange == 0x02),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_run_still_succeeds_when_the_run_announcement_fails()
    {
        var command = new StartRunCommand(
            "00000000-0000-0000-0000-000000000001", 100, ImuOdr.Hz104, AccelRange.G4, GyroRange.Dps500);
        _encoder.EncodeStartRun(command).Returns([0x02]);
        _runMetadataPublisher.PublishAsync(Arg.Any<RunMetadataEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("broker down"));

        using var commands = new CommandCounter();

        // Best-effort: the command already went out, so a failed announcement must not fail the run.
        await Should.NotThrowAsync(
            () => CreateSut().StartRunAsync(MixedCaseGuid, command, CancellationToken.None));
        commands.Count("start_run", "sent").ShouldBe(1);
    }

    [Fact]
    public async Task Disconnect_and_reset_publish_their_opcodes()
    {
        _encoder.EncodeDisconnect().Returns([0x03]);
        _encoder.EncodeReset().Returns([0x04]);

        using var commands = new CommandCounter();
        CommandService sut = CreateSut();
        await sut.DisconnectAsync(MixedCaseGuid, CancellationToken.None);
        await sut.ResetAsync(MixedCaseGuid, CancellationToken.None);

        await _publisher.Received(1).PublishAsync(
            MixedCaseGuid, Arg.Is<ReadOnlyMemory<byte>>(m => m.ToArray().SequenceEqual(new byte[] { 0x03 })),
            Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(
            MixedCaseGuid, Arg.Is<ReadOnlyMemory<byte>>(m => m.ToArray().SequenceEqual(new byte[] { 0x04 })),
            Arg.Any<CancellationToken>());
        commands.Count("disconnect", "sent").ShouldBe(1);
        commands.Count("reset", "sent").ShouldBe(1);
    }

    [Fact]
    public async Task A_transport_failure_is_counted_as_failed_and_rethrown()
    {
        _encoder.EncodeConnect().Returns([0x01]);
        _publisher.PublishAsync(Arg.Any<string>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("broker down"));

        using var commands = new CommandCounter();

        await Should.ThrowAsync<TimeoutException>(
            () => CreateSut().ConnectAsync(MixedCaseGuid, CancellationToken.None));

        commands.Count("connect", "failed").ShouldBe(1);
        commands.Count("connect", "sent").ShouldBe(0);
    }

    /// <summary>Listens to the command counter and tallies measurements by (command, outcome) tags.</summary>
    private sealed class CommandCounter : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly List<(string Command, string Outcome)> _measurements = [];

        public CommandCounter()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == ManagementMetrics.MeterName &&
                        instrument.Name == "racetracker_management_commands_total")
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                },
            };
            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                string command = "", outcome = "";
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    if (tag.Key == "command") command = tag.Value?.ToString() ?? "";
                    if (tag.Key == "outcome") outcome = tag.Value?.ToString() ?? "";
                }

                lock (_measurements)
                {
                    _measurements.Add((command, outcome));
                }
            });
            _listener.Start();
        }

        public int Count(string command, string outcome)
        {
            lock (_measurements)
            {
                return _measurements.Count(m => m.Command == command && m.Outcome == outcome);
            }
        }

        public void Dispose() => _listener.Dispose();
    }
}
