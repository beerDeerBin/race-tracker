using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Configuration;

namespace RaceTracker.Management.Infrastructure.Persistence;

/// <summary>
/// Real MongoDB connectivity probe (anti-stub): runs the <c>{ ping: 1 }</c> admin command against
/// the configured database through the shared <see cref="IMongoClient"/>. Throws if the store is
/// unreachable (server selection fails within the client's timeout). Reuses the pooled client
/// rather than opening a new connection per check.
/// </summary>
public sealed class MongoConnectivityCheck : IMongoConnectivityCheck
{
    private static readonly TimeSpan _pingTimeout = TimeSpan.FromSeconds(5);
    private static readonly BsonDocument _ping = new("ping", 1);

    private readonly IMongoClient _client;
    private readonly string _database;

    public MongoConnectivityCheck(IMongoClient client, IOptions<ManagementOptions> options)
    {
        _client = client;
        _database = options.Value.Mongo.Database;
    }

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_pingTimeout);

        await _client.GetDatabase(_database).RunCommandAsync<BsonDocument>(_ping, cancellationToken: timeout.Token);
    }
}
