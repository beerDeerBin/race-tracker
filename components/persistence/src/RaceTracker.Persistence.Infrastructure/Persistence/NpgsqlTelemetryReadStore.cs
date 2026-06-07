using System.Text;
using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Application.Telemetry;
using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Infrastructure.Persistence;

/// <summary>
/// Real Timescale read adapter (anti-stub) implementing <see cref="ITelemetryReadStore"/> over
/// Npgsql. It is the read half of the CQRS split (/F51/): it only ever <c>SELECT</c>s and shares no
/// state with the write repository. All inputs are bound as parameters; the sample query orders by
/// the integer time dimension (<c>sample_index</c>) and is bounded by a clamped <c>LIMIT</c>.
/// </summary>
public sealed class NpgsqlTelemetryReadStore : ITelemetryReadStore
{
    private const string RunsSql = """
        SELECT device_guid, run_id, num_samples, odr_hz, accel_range, gyro_range,
               started_at, ended_at, received_samples
        FROM runs
        WHERE device_guid = @g
        ORDER BY started_at DESC NULLS LAST, run_id;
        """;

    private readonly string _connectionString;

    public NpgsqlTelemetryReadStore(IOptions<PersistenceOptions> options)
        => _connectionString = TimescaleConnectionString.From(options.Value.Timescale);

    public async Task<IReadOnlyList<RunMetadata>> GetRunsAsync(
        Guid deviceGuid, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(RunsSql, connection);
        command.Parameters.AddWithValue("g", deviceGuid);

        var runs = new List<RunMetadata>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            runs.Add(new RunMetadata(
                DeviceGuid: reader.GetGuid(0),
                RunId: reader.GetGuid(1),
                NumSamples: GetNullable<int>(reader, 2),
                OdrHz: GetNullable<int>(reader, 3),
                AccelRange: GetNullable<short>(reader, 4),
                GyroRange: GetNullable<short>(reader, 5),
                StartedAt: GetNullable<DateTimeOffset>(reader, 6),
                EndedAt: GetNullable<DateTimeOffset>(reader, 7),
                ReceivedSamples: reader.GetInt32(8)));
        }

        return runs;
    }

    public async Task<IReadOnlyList<RunSample>> GetSamplesAsync(
        SampleQuery query, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand { Connection = connection };
        command.CommandText = BuildSamplesSql(query, command);

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

    public async Task<IReadOnlyList<SampleRollupBucket>> GetRunRollupAsync(
        RollupQuery query, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand { Connection = connection };
        command.CommandText = BuildRollupSql(query, command);

        var buckets = new List<SampleRollupBucket>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            buckets.Add(new SampleRollupBucket(
                BucketStartIndex: reader.GetInt64(0),
                Ax: ReadAxis(reader, 1),
                Ay: ReadAxis(reader, 4),
                Az: ReadAxis(reader, 7),
                Gx: ReadAxis(reader, 10),
                Gy: ReadAxis(reader, 13),
                Gz: ReadAxis(reader, 16),
                SampleCount: reader.GetInt64(19)));
        }

        return buckets;
    }

    public async Task<IReadOnlyList<TrajectoryPoint>> GetTrajectoryAsync(
        TrajectoryQuery query, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand { Connection = connection };
        command.CommandText = BuildTrajectorySql(query, command);

        var points = new List<TrajectoryPoint>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            points.Add(new TrajectoryPoint(
                Index: reader.GetInt64(0),
                T: reader.GetDouble(1),
                X: reader.GetDouble(2),
                Y: reader.GetDouble(3),
                Heading: reader.GetDouble(4)));
        }

        return points;
    }

    private static string BuildSamplesSql(SampleQuery query, NpgsqlCommand command)
    {
        var sql = new StringBuilder(
            "SELECT sample_index, ax, ay, az, gx, gy, gz FROM samples WHERE run_id = @r");
        command.Parameters.AddWithValue("r", query.RunId);

        if (query.DeviceGuid is { } deviceGuid)
        {
            sql.Append(" AND device_guid = @g");
            command.Parameters.AddWithValue("g", deviceGuid);
        }

        if (query.FromIndex is { } fromIndex)
        {
            sql.Append(" AND sample_index >= @from");
            command.Parameters.AddWithValue("from", fromIndex);
        }

        if (query.ToIndex is { } toIndex)
        {
            sql.Append(" AND sample_index <= @to");
            command.Parameters.AddWithValue("to", toIndex);
        }

        sql.Append(" ORDER BY sample_index LIMIT @limit;");
        command.Parameters.AddWithValue("limit", query.Limit);

        return sql.ToString();
    }

    private static string BuildRollupSql(RollupQuery query, NpgsqlCommand command)
    {
        // Column order must match the ReadAxis ordinals in GetRunRollupAsync.
        var sql = new StringBuilder(
            "SELECT bucket_start, ax_min, ax_max, ax_avg, ay_min, ay_max, ay_avg, "
            + "az_min, az_max, az_avg, gx_min, gx_max, gx_avg, gy_min, gy_max, gy_avg, "
            + "gz_min, gz_max, gz_avg, sample_count FROM samples_rollup WHERE run_id = @r");
        command.Parameters.AddWithValue("r", query.RunId);

        if (query.DeviceGuid is { } deviceGuid)
        {
            sql.Append(" AND device_guid = @g");
            command.Parameters.AddWithValue("g", deviceGuid);
        }

        if (query.FromIndex is { } fromIndex)
        {
            sql.Append(" AND bucket_start >= @from");
            command.Parameters.AddWithValue("from", fromIndex);
        }

        if (query.ToIndex is { } toIndex)
        {
            sql.Append(" AND bucket_start <= @to");
            command.Parameters.AddWithValue("to", toIndex);
        }

        sql.Append(" ORDER BY bucket_start LIMIT @limit;");
        command.Parameters.AddWithValue("limit", query.Limit);

        return sql.ToString();
    }

    private static string BuildTrajectorySql(TrajectoryQuery query, NpgsqlCommand command)
    {
        // Column order must match the ordinals read in GetTrajectoryAsync.
        var sql = new StringBuilder(
            "SELECT sample_index, t_seconds, x, y, heading FROM trajectory_points WHERE run_id = @r");
        command.Parameters.AddWithValue("r", query.RunId);

        if (query.DeviceGuid is { } deviceGuid)
        {
            sql.Append(" AND device_guid = @g");
            command.Parameters.AddWithValue("g", deviceGuid);
        }

        if (query.FromIndex is { } fromIndex)
        {
            sql.Append(" AND sample_index >= @from");
            command.Parameters.AddWithValue("from", fromIndex);
        }

        if (query.ToIndex is { } toIndex)
        {
            sql.Append(" AND sample_index <= @to");
            command.Parameters.AddWithValue("to", toIndex);
        }

        if (query.Stride > 1)
        {
            // Downsample to every stride-th point; index 0 is on a boundary so the origin is kept.
            sql.Append(" AND sample_index % @stride = 0");
            command.Parameters.AddWithValue("stride", (long)query.Stride);
        }

        sql.Append(" ORDER BY sample_index LIMIT @limit;");
        command.Parameters.AddWithValue("limit", query.Limit);

        return sql.ToString();
    }

    // Reads a {min, max, avg} axis triple starting at the given ordinal: min/max are real (float),
    // avg is double precision (Postgres avg(real)).
    private static AxisRollup ReadAxis(NpgsqlDataReader reader, int ordinal)
        => new(reader.GetFloat(ordinal), reader.GetFloat(ordinal + 1), reader.GetDouble(ordinal + 2));

    private static T? GetNullable<T>(NpgsqlDataReader reader, int ordinal)
        where T : struct
        => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);
}
