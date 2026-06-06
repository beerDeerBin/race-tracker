namespace RaceTracker.Management.Application.Configuration;

/// <summary>
/// Strongly-typed management configuration (<c>/A40/</c>), bound once from the
/// <see cref="Section"/> section and consumed via <c>IOptions&lt;ManagementOptions&gt;</c>.
/// </summary>
public sealed class ManagementOptions
{
    public const string Section = "Management";

    /// <summary>Document store (MongoDB) for the management domain entities (User/Vehicle).</summary>
    public MongoOptions Mongo { get; init; } = new();
}

/// <summary>
/// MongoDB connection settings. Credentials are optional: the local dev stack runs Mongo
/// without auth, so <see cref="Username"/>/<see cref="Password"/> stay empty and are only
/// woven into the connection string when set (see <c>MongoConnectionString</c>).
/// </summary>
public sealed class MongoOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 27017;
    public string Database { get; init; } = "racetracker";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
}
