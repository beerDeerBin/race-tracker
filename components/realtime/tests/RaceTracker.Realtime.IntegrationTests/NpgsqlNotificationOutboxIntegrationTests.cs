using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Outbox;
using RaceTracker.Realtime.Application.Rules;
using RaceTracker.Realtime.Infrastructure.Migrations;
using RaceTracker.Realtime.Infrastructure.Persistence;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace RaceTracker.Realtime.IntegrationTests;

/// <summary>
/// High-fidelity test of the real PostgreSQL outbox adapter (story 8.3 AK) against a real Postgres
/// in a throwaway container — no mocks. Runs the actual migrations + repository and asserts the
/// durable lifecycle (enqueue → dequeue → mark dispatched), idempotent enqueue (no duplicate row
/// for the same logical event), and **restart survival**: a fresh adapter instance over the same
/// database still sees a pending row and can dispatch it. Requires Docker.
/// </summary>
public sealed class NpgsqlNotificationOutboxIntegrationTests : IAsyncLifetime
{
    private const string Image = "postgres:16-alpine";
    private const string Database = "racetracker";
    private const string User = "race";
    private const string Pass = "race";
    private const string Guid = "00000000-0000-0000-0000-0000000000aa";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder(Image)
        .WithDatabase(Database)
        .WithUsername(User)
        .WithPassword(Pass)
        .Build();

    public Task InitializeAsync() => _db.StartAsync();

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Enqueue_dequeue_and_mark_dispatched_form_the_durable_lifecycle()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        IOptions<RealtimeOptions> options = await MigratedOptionsAsync(cts.Token);
        var outbox = new NpgsqlNotificationOutbox(options);

        await outbox.EnqueueAsync(Event(DateTimeOffset.UnixEpoch), cts.Token);

        IReadOnlyList<OutboxMessage> pending = await outbox.DequeuePendingAsync(50, cts.Token);
        OutboxMessage message = pending.ShouldHaveSingleItem();
        message.Type.ShouldBe(RuleType.BatteryCritical);
        message.DeviceGuid.ShouldBe(Guid);

        await outbox.MarkDispatchedAsync(message.Id, cts.Token);

        // Once dispatched it is no longer pending.
        (await outbox.DequeuePendingAsync(50, cts.Token)).ShouldBeEmpty();
        (await CountAsync(options, "SELECT count(*) FROM notification_outbox WHERE status='Dispatched';",
            cts.Token)).ShouldBe(1L);
    }

    [Fact]
    public async Task Re_enqueuing_the_same_logical_event_writes_no_duplicate_row()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        IOptions<RealtimeOptions> options = await MigratedOptionsAsync(cts.Token);
        var outbox = new NpgsqlNotificationOutbox(options);

        RuleEvent fired = Event(DateTimeOffset.UnixEpoch);
        await outbox.EnqueueAsync(fired, cts.Token);
        await outbox.EnqueueAsync(fired, cts.Token); // same type/device/fired-at → same dedup key

        (await CountAsync(options, "SELECT count(*) FROM notification_outbox;", cts.Token))
            .ShouldBe(1L);
    }

    [Fact]
    public async Task A_pending_row_survives_a_service_restart()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        IOptions<RealtimeOptions> options = await MigratedOptionsAsync(cts.Token);

        // "Before the restart": enqueue but never dispatch.
        await new NpgsqlNotificationOutbox(options).EnqueueAsync(Event(DateTimeOffset.UnixEpoch), cts.Token);

        // "After the restart": a brand-new adapter (and a re-run of the idempotent migrator) over
        // the same durable database still sees the pending row and can dispatch it.
        await new NpgsqlOutboxMigrator(options, NullLogger<NpgsqlOutboxMigrator>.Instance)
            .MigrateAsync(cts.Token);
        var afterRestart = new NpgsqlNotificationOutbox(options);

        OutboxMessage survived = (await afterRestart.DequeuePendingAsync(50, cts.Token))
            .ShouldHaveSingleItem();
        await afterRestart.MarkDispatchedAsync(survived.Id, cts.Token);

        (await afterRestart.DequeuePendingAsync(50, cts.Token)).ShouldBeEmpty();
    }

    private async Task<IOptions<RealtimeOptions>> MigratedOptionsAsync(CancellationToken cancellationToken)
    {
        IOptions<RealtimeOptions> options = Options.Create(new RealtimeOptions
        {
            Outbox = new OutboxOptions
            {
                Host = _db.Hostname,
                Port = _db.GetMappedPublicPort(5432),
                Database = Database,
                Username = User,
                Password = Pass,
            },
        });

        await new NpgsqlOutboxMigrator(options, NullLogger<NpgsqlOutboxMigrator>.Instance)
            .MigrateAsync(cancellationToken);
        return options;
    }

    private static async Task<long> CountAsync(
        IOptions<RealtimeOptions> options, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(
            OutboxConnectionString.From(options.Value.Outbox));
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static RuleEvent Event(DateTimeOffset firedAt) =>
        new(RuleType.BatteryCritical, Guid, $"Battery critical on device {Guid}", firedAt);
}
