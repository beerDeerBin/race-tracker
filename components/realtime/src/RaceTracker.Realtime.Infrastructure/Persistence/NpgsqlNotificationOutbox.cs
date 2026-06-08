using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Outbox;
using RaceTracker.Realtime.Application.Rules;

namespace RaceTracker.Realtime.Infrastructure.Persistence;

/// <summary>
/// Real PostgreSQL notification-outbox adapter (anti-stub, story 8.3). <see cref="EnqueueAsync"/>
/// records a fired event as a Pending row in one atomic statement, idempotent on a natural
/// <c>dedup_key</c> (<c>{type}:{guid}:{firedAt}</c>) via <c>ON CONFLICT DO NOTHING</c>.
/// <see cref="DequeuePendingAsync"/> reads the pending tail oldest-first; <see cref="MarkDispatchedAsync"/>
/// flips a row to Dispatched only after a successful push — so delivery survives a service restart.
/// </summary>
public sealed class NpgsqlNotificationOutbox : INotificationOutbox
{
    private readonly string _connectionString;

    public NpgsqlNotificationOutbox(IOptions<RealtimeOptions> options)
        => _connectionString = OutboxConnectionString.From(options.Value.Outbox);

    public async Task EnqueueAsync(RuleEvent ruleEvent, CancellationToken cancellationToken)
    {
        // Round-trippable, sortable timestamp keeps the dedup key stable for the same logical event.
        string dedupKey = $"{ruleEvent.Type}:{ruleEvent.DeviceGuid}:{ruleEvent.FiredAtUtc:O}";

        const string sql = """
            INSERT INTO notification_outbox (dedup_key, device_guid, rule_type, message, fired_at)
            VALUES (@dedup_key, @device_guid, @rule_type, @message, @fired_at)
            ON CONFLICT (dedup_key) DO NOTHING;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("dedup_key", dedupKey);
        command.Parameters.AddWithValue("device_guid", ruleEvent.DeviceGuid);
        command.Parameters.AddWithValue("rule_type", ruleEvent.Type.ToString());
        command.Parameters.AddWithValue("message", ruleEvent.Message);
        command.Parameters.AddWithValue("fired_at", ruleEvent.FiredAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> DequeuePendingAsync(
        int batchSize, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, rule_type, device_guid, message, fired_at
            FROM notification_outbox
            WHERE status = 'Pending'
            ORDER BY created_at
            LIMIT @batch;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("batch", batchSize);

        var pending = new List<OutboxMessage>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            pending.Add(new OutboxMessage(
                reader.GetInt64(0),
                Enum.Parse<RuleType>(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }

        return pending;
    }

    public async Task MarkDispatchedAsync(long id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE notification_outbox
            SET status = 'Dispatched', dispatched_at = now()
            WHERE id = @id;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
