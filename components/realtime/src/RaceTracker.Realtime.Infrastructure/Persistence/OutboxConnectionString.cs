using Npgsql;
using RaceTracker.Realtime.Application.Configuration;

namespace RaceTracker.Realtime.Infrastructure.Persistence;

/// <summary>
/// Assembles the Npgsql connection string for the outbox store from the discrete
/// <see cref="OutboxOptions"/> settings (story 8.3). Lives in Infrastructure (Npgsql is the
/// adapter's tech, kept out of the inner layers) and uses <see cref="NpgsqlConnectionStringBuilder"/>
/// so values containing reserved characters (e.g. <c>;</c> or <c>=</c>) are escaped, not corrupted.
/// </summary>
public static class OutboxConnectionString
{
    public static string From(OutboxOptions options) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Database,
            Username = options.Username,
            Password = options.Password,
        }.ConnectionString;
}
