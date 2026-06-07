using Npgsql;
using RaceTracker.Persistence.Application.Configuration;

namespace RaceTracker.Persistence.Infrastructure.Persistence;

/// <summary>
/// Assembles the Npgsql connection string from the discrete <see cref="TimescaleOptions"/>
/// settings. Lives in Infrastructure (Npgsql is the persistence adapter's tech, kept out of
/// the inner layers) and uses <see cref="NpgsqlConnectionStringBuilder"/> so values that
/// contain reserved characters (e.g. <c>;</c> or <c>=</c>) are escaped, not corrupted.
/// </summary>
public static class TimescaleConnectionString
{
    public static string From(TimescaleOptions options) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Database,
            Username = options.Username,
            Password = options.Password,
        }.ConnectionString;
}
