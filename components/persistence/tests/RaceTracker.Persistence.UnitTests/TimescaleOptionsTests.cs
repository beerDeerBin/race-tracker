using Npgsql;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

public sealed class TimescaleOptionsTests
{
    [Fact]
    public void Builds_a_well_formed_npgsql_connection_string_from_discrete_settings()
    {
        var options = new TimescaleOptions
        {
            Host = "timescaledb",
            Port = 5432,
            Database = "racetracker",
            Username = "race",
            Password = "secret",
        };

        // Round-trip through Npgsql's parser so the assertion is independent of key order/format.
        var parsed = new NpgsqlConnectionStringBuilder(TimescaleConnectionString.From(options));

        parsed.Host.ShouldBe("timescaledb");
        parsed.Port.ShouldBe(5432);
        parsed.Database.ShouldBe("racetracker");
        parsed.Username.ShouldBe("race");
        parsed.Password.ShouldBe("secret");
    }

    [Fact]
    public void Escapes_reserved_characters_in_settings_so_the_connection_string_is_not_corrupted()
    {
        var options = new TimescaleOptions { Password = "p;w=d" };

        var parsed = new NpgsqlConnectionStringBuilder(TimescaleConnectionString.From(options));

        parsed.Password.ShouldBe("p;w=d");
    }
}
