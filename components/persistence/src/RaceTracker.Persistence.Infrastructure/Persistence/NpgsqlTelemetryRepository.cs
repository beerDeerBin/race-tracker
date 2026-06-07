using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Infrastructure.Persistence;

/// <summary>
/// Real Timescale write adapter (anti-stub) implementing <see cref="ITelemetryRepository"/> over
/// Npgsql. One transaction per batch: a multi-row sample insert with
/// <c>ON CONFLICT (...) DO NOTHING</c> (idempotent — re-delivering the same
/// <c>guid/runId/index</c> writes nothing, /A60/), then a run-metadata upsert whose
/// <c>received_samples</c> is <b>recomputed</b> from the actual stored rows in the same
/// transaction, so the count is correct regardless of redelivery. The sample-driven upsert touches
/// only <c>received_samples</c>/<c>started_at</c>/<c>ended_at</c>; the parameter columns
/// (<c>odr_hz</c>, accel/gyro range, requested <c>num_samples</c>) are filled by
/// <see cref="UpsertRunMetadataAsync"/> from management's run announcement (the device never echoes
/// the ODR back). The two upserts touch disjoint columns, so they merge in any order.
/// </summary>
public sealed class NpgsqlTelemetryRepository : ITelemetryRepository
{
    private const string RunUpsertSql = """
        INSERT INTO runs (device_guid, run_id, received_samples, started_at, ended_at, updated_at)
        VALUES (@g, @r,
                (SELECT count(*) FROM samples WHERE device_guid = @g AND run_id = @r),
                @observed, @observed, now())
        ON CONFLICT (device_guid, run_id) DO UPDATE SET
            received_samples = (SELECT count(*) FROM samples
                                WHERE device_guid = @g AND run_id = @r),
            started_at = LEAST(runs.started_at, EXCLUDED.started_at),
            ended_at   = GREATEST(runs.ended_at, EXCLUDED.ended_at),
            updated_at = now();
        """;

    // Fills the parameter columns from management's announcement, keyed by (device_guid, run_id).
    // Deliberately leaves received_samples / ended_at alone (the sample path owns them); started_at
    // takes the earliest of the announced start and any sample-derived start (LEAST ignores NULLs).
    private const string RunMetadataUpsertSql = """
        INSERT INTO runs (device_guid, run_id, num_samples, odr_hz, accel_range, gyro_range,
                          started_at, updated_at)
        VALUES (@g, @r, @num, @odr, @accel, @gyro, @started, now())
        ON CONFLICT (device_guid, run_id) DO UPDATE SET
            num_samples = EXCLUDED.num_samples,
            odr_hz      = EXCLUDED.odr_hz,
            accel_range = EXCLUDED.accel_range,
            gyro_range  = EXCLUDED.gyro_range,
            started_at  = LEAST(runs.started_at, EXCLUDED.started_at),
            updated_at  = now();
        """;

    private readonly string _connectionString;

    public NpgsqlTelemetryRepository(IOptions<PersistenceOptions> options)
        => _connectionString = TimescaleConnectionString.From(options.Value.Timescale);

    public async Task<int> UpsertSampleBatchAsync(SampleBatch batch, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlTransaction transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        int inserted = await InsertSamplesAsync(connection, transaction, batch, cancellationToken);
        await UpsertRunAsync(connection, transaction, batch, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }

    public async Task UpsertRunMetadataAsync(RunParameters run, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(RunMetadataUpsertSql, connection);
        command.Parameters.AddWithValue("g", run.DeviceGuid);
        command.Parameters.AddWithValue("r", run.RunId);
        command.Parameters.AddWithValue("num", run.NumSamples);
        command.Parameters.AddWithValue("odr", run.OdrHz);
        command.Parameters.AddWithValue("accel", run.AccelRange);
        command.Parameters.AddWithValue("gyro", run.GyroRange);
        command.Parameters.AddWithValue("started", run.StartedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> InsertSamplesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, SampleBatch batch,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder(
            "INSERT INTO samples (device_guid, run_id, sample_index, ax, ay, az, gx, gy, gz) VALUES ");

        await using var command = new NpgsqlCommand { Connection = connection, Transaction = transaction };
        command.Parameters.AddWithValue("g", batch.DeviceGuid);
        command.Parameters.AddWithValue("r", batch.RunId);

        for (int i = 0; i < batch.Samples.Count; i++)
        {
            RunSample sample = batch.Samples[i];
            string n = i.ToString(CultureInfo.InvariantCulture);

            if (i > 0)
            {
                sql.Append(',');
            }

            sql.Append("(@g,@r,@i").Append(n)
                .Append(",@ax").Append(n).Append(",@ay").Append(n).Append(",@az").Append(n)
                .Append(",@gx").Append(n).Append(",@gy").Append(n).Append(",@gz").Append(n).Append(')');

            command.Parameters.AddWithValue("i" + n, sample.Index);
            command.Parameters.AddWithValue("ax" + n, sample.Reading.Ax);
            command.Parameters.AddWithValue("ay" + n, sample.Reading.Ay);
            command.Parameters.AddWithValue("az" + n, sample.Reading.Az);
            command.Parameters.AddWithValue("gx" + n, sample.Reading.Gx);
            command.Parameters.AddWithValue("gy" + n, sample.Reading.Gy);
            command.Parameters.AddWithValue("gz" + n, sample.Reading.Gz);
        }

        // Idempotent: a re-delivered sample (same composite key) is silently skipped. The affected
        // count is the number of rows actually inserted (skipped conflicts don't count).
        sql.Append(" ON CONFLICT (device_guid, run_id, sample_index) DO NOTHING;");
        command.CommandText = sql.ToString();

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertRunAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, SampleBatch batch,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(RunUpsertSql, connection, transaction);
        command.Parameters.AddWithValue("g", batch.DeviceGuid);
        command.Parameters.AddWithValue("r", batch.RunId);
        command.Parameters.AddWithValue("observed", batch.ObservedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
