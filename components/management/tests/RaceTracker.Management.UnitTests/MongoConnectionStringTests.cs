using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

public sealed class MongoConnectionStringTests
{
    [Fact]
    public void Omits_credentials_when_no_username_is_configured()
    {
        var options = new MongoOptions { Host = "mongodb", Port = 27017, Database = "racetracker" };

        MongoConnectionString.From(options).ShouldBe("mongodb://mongodb:27017");
    }

    [Fact]
    public void Includes_escaped_credentials_when_a_username_is_configured()
    {
        var options = new MongoOptions
        {
            Host = "mongodb",
            Port = 27017,
            Database = "racetracker",
            Username = "race",
            Password = "p@ss",
        };

        MongoConnectionString.From(options).ShouldBe("mongodb://race:p%40ss@mongodb:27017");
    }
}
