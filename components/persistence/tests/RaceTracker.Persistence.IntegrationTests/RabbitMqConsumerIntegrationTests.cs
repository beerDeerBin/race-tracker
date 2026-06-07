using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Application.Observability;
using RaceTracker.Persistence.Application.Telemetry;
using RaceTracker.Persistence.Infrastructure.Messaging;
using RaceTracker.Persistence.Infrastructure.Migrations;
using RaceTracker.Persistence.Infrastructure.Persistence;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace RaceTracker.Persistence.IntegrationTests;

/// <summary>
/// End-to-end test of the real RabbitMQ consumer (story 3.3 AK) against a real broker + real
/// TimescaleDB in throwaway containers — no mocks. It drives the actual
/// <see cref="RabbitMqTelemetryConsumer"/>: a full run published as ≤32-sample batches lands in
/// Timescale with the exact sample count and one run-metadata record, while a poison message is
/// dead-lettered (rejected without requeue). Requires Docker.
/// </summary>
public sealed class RabbitMqConsumerIntegrationTests : IAsyncLifetime
{
    private const string TimescaleImage = "timescale/timescaledb:2.17.2-pg16";
    private const string Database = "racetracker";
    private const string User = "race";
    private const string Pass = "race";
    private const string Vhost = "race-tracker";

    private const string DeviceGuid = "00000000-0000-0000-0000-0000000000aa";
    private const string RunId = "11111111-1111-1111-1111-111111111111";
    private const int TotalSamples = 8330;
    private const int BatchSize = 32;

    private const string Queue = "rt.persistence.samples";
    private const string DeadLetterExchange = "rt.persistence.dlx";
    private const string DeadLetterQueue = "rt.persistence.dlq";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder(TimescaleImage)
        .WithDatabase(Database)
        .WithUsername(User)
        .WithPassword(Pass)
        .Build();

    private readonly IContainer _rabbit = new ContainerBuilder("rabbitmq:4-management")
        .WithEnvironment("RABBITMQ_DEFAULT_USER", User)
        .WithEnvironment("RABBITMQ_DEFAULT_PASS", Pass)
        .WithEnvironment("RABBITMQ_DEFAULT_VHOST", Vhost)
        .WithPortBinding(5672, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_db.StartAsync(), _rabbit.StartAsync());
        await new NpgsqlDatabaseMigrator(BuildOptions(), NullLogger<NpgsqlDatabaseMigrator>.Instance)
            .MigrateAsync(CancellationToken.None);
    }

    public Task DisposeAsync() =>
        Task.WhenAll(_db.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());

    [Fact]
    public async Task Full_run_is_persisted_with_correct_count_and_poison_is_dead_lettered()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        IOptions<PersistenceOptions> options = BuildOptions();

        // Declare the topology + publish BEFORE the consumer starts, so a durable queue holds the
        // messages (no topic-exchange drop race). The consumer re-declares the same topology.
        var factory = new ConnectionFactory
        {
            HostName = _rabbit.Hostname,
            Port = _rabbit.GetMappedPublicPort(5672),
            VirtualHost = Vhost,
            UserName = User,
            Password = Pass,
        };
        await using (IConnection connection = await factory.CreateConnectionAsync(cts.Token))
        await using (IChannel channel = await connection.CreateChannelAsync(cancellationToken: cts.Token))
        {
            await DeclareTopologyAsync(channel, cts.Token);
            await PublishFullRunAsync(channel, cts.Token);
            await PublishPoisonAsync(channel, cts.Token);
        }

        // Drive the real consumer adapter.
        var consumer = new RabbitMqTelemetryConsumer(
            options,
            new SampleBatchIngestService(new NpgsqlTelemetryRepository(options)),
            new PersistenceMetrics(),
            NullLogger<RabbitMqTelemetryConsumer>.Instance);

        await consumer.StartAsync(cts.Token);
        try
        {
            await using var db = new NpgsqlConnection(
                TimescaleConnectionString.From(options.Value.Timescale));
            await db.OpenAsync(cts.Token);

            // Full-run AK: every sample is persisted exactly once.
            await WaitForAsync(
                async () => (long)(await ScalarAsync(db,
                    $"SELECT count(*) FROM samples WHERE run_id = '{RunId}';", cts.Token))! == TotalSamples,
                cts.Token);

            (await ScalarAsync(db,
                $"SELECT count(*) FROM runs WHERE run_id = '{RunId}';", cts.Token)).ShouldBe(1L);
            (await ScalarAsync(db,
                $"SELECT received_samples FROM runs WHERE run_id = '{RunId}';", cts.Token))
                .ShouldBe(TotalSamples);

            // Dead-letter AK: the poison message is rejected without requeue → DLQ.
            await using IConnection check = await factory.CreateConnectionAsync(cts.Token);
            await using IChannel checkChannel = await check.CreateChannelAsync(cancellationToken: cts.Token);
            await WaitForAsync(
                async () => await checkChannel.MessageCountAsync(DeadLetterQueue, cts.Token) >= 1,
                cts.Token);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    private static async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            DeadLetterQueue, DeadLetterExchange, routingKey: string.Empty,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            TelemetryExchanges.Data, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
        };
        await channel.QueueDeclareAsync(
            Queue, durable: true, exclusive: false, autoDelete: false, arguments: arguments,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            Queue, TelemetryExchanges.Data, routingKey: "#", cancellationToken: cancellationToken);
    }

    private static async Task PublishFullRunAsync(IChannel channel, CancellationToken cancellationToken)
    {
        var observedAt = DateTimeOffset.UtcNow;

        for (uint offset = 0; offset < TotalSamples; offset += BatchSize)
        {
            uint count = (uint)Math.Min(BatchSize, TotalSamples - offset);
            var samples = new List<ImuSampleDto>((int)count);
            for (uint i = 0; i < count; i++)
            {
                samples.Add(new ImuSampleDto(offset + i, 0, 9.81f, 0, 0, 0));
            }

            var batch = new SampleBatchEvent(DeviceGuid, RunId, offset, count, samples, observedAt);
            await PublishAsync(channel, JsonSerializer.SerializeToUtf8Bytes(batch), cancellationToken);
        }
    }

    private static Task PublishPoisonAsync(IChannel channel, CancellationToken cancellationToken)
        // Valid JSON, but a non-UUID runId → TelemetryValidationException → dead-letter.
        => PublishAsync(channel, Encoding.UTF8.GetBytes(
            $$"""{"DeviceGuid":"{{DeviceGuid}}","RunId":"run-1","StartOffset":0,"Count":1,"Samples":[{"Ax":1,"Ay":2,"Az":3,"Gx":4,"Gy":5,"Gz":6}],"ObservedAtUtc":"2026-01-01T00:00:00+00:00"}"""),
            cancellationToken);

    private static async Task PublishAsync(
        IChannel channel, byte[] body, CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString("N"),
            Type = "data",
        };

        await channel.BasicPublishAsync(
            TelemetryExchanges.Data, DeviceGuid, mandatory: false, basicProperties: properties,
            body: body, cancellationToken: cancellationToken);
    }

    private static async Task WaitForAsync(
        Func<Task<bool>> condition, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException("Condition was not met before the deadline.");
    }

    private IOptions<PersistenceOptions> BuildOptions() => Options.Create(new PersistenceOptions
    {
        RabbitMq = new RabbitMqOptions
        {
            Host = _rabbit.Hostname,
            Port = _rabbit.GetMappedPublicPort(5672),
            VirtualHost = Vhost,
            Username = User,
            Password = Pass,
        },
        Timescale = new TimescaleOptions
        {
            Host = _db.Hostname,
            Port = _db.GetMappedPublicPort(5432),
            Database = Database,
            Username = User,
            Password = Pass,
        },
    });

    private static async Task<object?> ScalarAsync(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
