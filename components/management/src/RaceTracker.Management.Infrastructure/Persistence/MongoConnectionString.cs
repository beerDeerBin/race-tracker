using RaceTracker.Management.Application.Configuration;

namespace RaceTracker.Management.Infrastructure.Persistence;

/// <summary>
/// Builds the MongoDB connection URI from <see cref="MongoOptions"/>. Credentials are woven in
/// only when a username is configured, so the auth-less local dev stack yields a plain
/// <c>mongodb://host:port</c> while a secured deployment gets <c>mongodb://user:pass@host:port</c>.
/// </summary>
public static class MongoConnectionString
{
    public static string From(MongoOptions options)
    {
        var credentials = string.IsNullOrEmpty(options.Username)
            ? string.Empty
            : $"{Uri.EscapeDataString(options.Username)}:{Uri.EscapeDataString(options.Password)}@";

        return $"mongodb://{credentials}{options.Host}:{options.Port}";
    }
}
