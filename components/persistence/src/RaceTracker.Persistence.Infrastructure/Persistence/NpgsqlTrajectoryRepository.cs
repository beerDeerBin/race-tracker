using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Application.Telemetry;
using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Infrastructure.Persistence;

/// <summary>
/// Real Timescale projection adapter (anti-stub) implementing <see cref="ITrajectoryRepository"/>
/// over Npgsql (story 4.3). Finds runs whose derived trajectory is stale, loads their raw samples
/// for recomputation, and idempotently (re)persists the computed points into the derived
/// <c>trajectory_points</c> table — <b>never</b> touching the raw <c>samples</c>. A rebuild is a
/// <c>DELETE</c> + chunked re-<c>INSERT</c> in one transaction, so it is safe to run repeatedly and
/// always reflects the current samples.
/// </summary>
public sealed class NpgsqlTrajectoryRepository : ITrajectoryRepository
{
    // Each point binds 5 parameters (index, t, x, y, heading) plus the 2 shared keys; 500 rows
    // keeps a chunk's parameter count well under Npgsql's 65535 cap.
    private const int InsertChunkSize = 500;

    private const string PendingRunsSql = """
        SELECT r.device_guid, r.run_id, r.odr_hz
        FROM runs r
        WHERE r.received_samples > 0
          AND COALESCE(r.updated_at, r.created_at) < now() - @settle
          AND r.received_samples <> (
              SELECT count(*) FROM trajectory_points t
              WHERE t.device_guid = r.device_guid AND t.run_id = r.run_id)
        ORDER BY r.device_guid, r.run_id;
        """;

    private const string LoadSamplesSql = """
        SELECT sample_index, ax, ay, az, gx, gy, gz
        FROM samples
        WHERE device_guid = @g AND run_id = @r
        ORDER BY sample_index;
        """;

    private readonly string _connectionString;

    public NpgsqlTrajectoryRepository(IOptions<PersistenceOptions> options)
        => _connectionString = TimescaleConnectionString.From(options.Value.Timescale);

    public async Task<IReadOnlyList<PendingRun>> GetRunsNeedingTrajectoryAsync(
        TimeSpan settle, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(PendingRunsSql, connection);
        command.Parameters.AddWithValue("settle", settle);

        var runs = new List<PendingRun>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            runs.Add(new PendingRun(
                DeviceGuid: reader.GetGuid(0),
                RunId: reader.GetGuid(1),
                OdrHz: reader.IsDBNull(2) ? null : reader.GetInt32(2)));
        }

        return runs;
    }

    public async Task<IReadOnlyList<RunSample>> LoadSamplesAsync(
        Guid deviceGuid, Guid runId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(LoadSamplesSql, connection);
        command.Parameters.AddWithValue("g", deviceGuid);
        command.Parameters.AddWithValue("r", runId);

        var samples = new List<RunSample>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var reading = new ImuReading(
                reader.GetFloat(1), reader.GetFloat(2), reader.GetFloat(3),
                reader.GetFloat(4), reader.GetFloat(5), reader.GetFloat(6));
            samples.Add(new RunSample(reader.GetInt64(0), reading));
        }

        return samples;
    }

    public async Task<int> SaveAsync(RunTrajectory trajectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trajectory);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlTransaction transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        await DeleteExistingAsync(connection, transaction, trajectory, cancellationToken);

        IReadOnlyList<TrajectoryPoint> points = trajectory.Points;
        for (int offset = 0; offset < points.Count; offset += InsertChunkSize)
        {
            int count = Math.Min(InsertChunkSize, points.Count - offset);
            await InsertChunkAsync(
                connection, transaction, trajectory, offset, count, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return points.Count;
    }

    private static async Task DeleteExistingAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, RunTrajectory trajectory,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "DELETE FROM trajectory_points WHERE device_guid = @g AND run_id = @r;",
            connection, transaction);
        command.Parameters.AddWithValue("g", trajectory.DeviceGuid);
        command.Parameters.AddWithValue("r", trajectory.RunId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertChunkAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, RunTrajectory trajectory,
        int offset, int count, CancellationToken cancellationToken)
    {
        var sql = new StringBuilder(
            "INSERT INTO trajectory_points "
            + "(device_guid, run_id, sample_index, t_seconds, x, y, heading) VALUES ");

        await using var command = new NpgsqlCommand { Connection = connection, Transaction = transaction };
        command.Parameters.AddWithValue("g", trajectory.DeviceGuid);
        command.Parameters.AddWithValue("r", trajectory.RunId);

        for (int i = 0; i < count; i++)
        {
            TrajectoryPoint p = trajectory.Points[offset + i];
            string n = i.ToString(CultureInfo.InvariantCulture);

            if (i > 0)
            {
                sql.Append(',');
            }

            sql.Append("(@g,@r,@i").Append(n)
                .Append(",@t").Append(n).Append(",@x").Append(n)
                .Append(",@y").Append(n).Append(",@h").Append(n).Append(')');

            command.Parameters.AddWithValue("i" + n, p.Index);
            command.Parameters.AddWithValue("t" + n, p.T);
            command.Parameters.AddWithValue("x" + n, p.X);
            command.Parameters.AddWithValue("y" + n, p.Y);
            command.Parameters.AddWithValue("h" + n, p.Heading);
        }

        sql.Append(';');
        command.CommandText = sql.ToString();
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
