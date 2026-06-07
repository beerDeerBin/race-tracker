using Npgsql;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace RaceTracker.Realtime.UnitTests;

/// <summary>
/// Unit test for the outbox connection-string builder (story 8.3): the discrete
/// <see cref="OutboxOptions"/> map onto the expected Npgsql parameters, with reserved characters
/// (e.g. <c>;</c> in the password) escaped rather than corrupted.
/// </summary>
public sealed class OutboxConnectionStringTests
{
    [Fact]
    public void Builds_the_expected_npgsql_parameters_and_escapes_reserved_chars()
    {
        var options = new OutboxOptions
        {
            Host = "postgres",
            Port = 5432,
            Database = "racetracker",
            Username = "race",
            Password = "p;a=ss", // reserved chars must survive a round-trip
        };

        string connectionString = OutboxConnectionString.From(options);

        var parsed = new NpgsqlConnectionStringBuilder(connectionString);
        parsed.Host.ShouldBe("postgres");
        parsed.Port.ShouldBe(5432);
        parsed.Database.ShouldBe("racetracker");
        parsed.Username.ShouldBe("race");
        parsed.Password.ShouldBe("p;a=ss");
    }
}
