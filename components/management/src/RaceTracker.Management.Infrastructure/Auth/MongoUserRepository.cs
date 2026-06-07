using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Domain.Auth;

namespace RaceTracker.Management.Infrastructure.Auth;

/// <summary>
/// Real MongoDB-backed user store (anti-stub). Persists to the <c>users</c> collection through a
/// dedicated <see cref="UserDocument"/> so the Domain <see cref="User"/> stays free of persistence
/// concerns. Lookups are by the unique login name; seeding upserts idempotently and never
/// overwrites an existing account's password (§8 Sicherheit).
/// </summary>
public sealed class MongoUserRepository : IUserRepository
{
    private const string CollectionName = "users";

    private readonly IMongoCollection<UserDocument> _collection;

    public MongoUserRepository(IMongoClient client, IOptions<ManagementOptions> options)
    {
        IMongoDatabase database = client.GetDatabase(options.Value.Mongo.Database);
        _collection = database.GetCollection<UserDocument>(CollectionName);
    }

    public async Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        UserDocument? document = await _collection
            .Find(d => d.Username == username)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : ToDomain(document);
    }

    public async Task EnsureSeededAsync(User user, CancellationToken cancellationToken)
    {
        await EnsureUsernameIndexAsync(cancellationToken);

        UserDocument? existing = await _collection
            .Find(d => d.Username == user.Username)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return;
        }

        try
        {
            await _collection.InsertOneAsync(ToDocument(user), options: null, cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another instance seeded the same account concurrently — idempotent no-op.
        }
    }

    private async Task EnsureUsernameIndexAsync(CancellationToken cancellationToken)
    {
        IndexKeysDefinition<UserDocument> keys = Builders<UserDocument>.IndexKeys.Ascending(d => d.Username);
        var model = new CreateIndexModel<UserDocument>(keys, new CreateIndexOptions { Unique = true });
        await _collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }

    private static User ToDomain(UserDocument document) => new()
    {
        Id = document.Id,
        Username = document.Username,
        PasswordHash = document.PasswordHash,
        Role = document.Role,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc), TimeSpan.Zero),
    };

    private static UserDocument ToDocument(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        PasswordHash = user.PasswordHash,
        Role = user.Role,
        CreatedAt = user.CreatedAt.UtcDateTime,
    };
}

/// <summary>Persistence shape for <see cref="User"/> in the <c>users</c> collection.</summary>
internal sealed class UserDocument
{
    [BsonId]
    public string Id { get; set; } = "";

    [BsonElement("username")]
    public string Username { get; set; } = "";

    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = "";

    [BsonElement("role")]
    public string Role { get; set; } = "";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
