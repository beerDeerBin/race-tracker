using System.Linq.Expressions;
using MongoDB.Driver;
using RaceTracker.Management.Application.Abstractions.Crud;
using RaceTracker.Management.Domain.Abstractions;

namespace RaceTracker.Management.Infrastructure.Persistence.Crud;

/// <summary>
/// Real MongoDB-backed generic repository (<c>/A70/</c>, anti-stub). Persists the domain entity
/// directly via the registered BSON class map (see <see cref="ManagementBsonConfiguration"/>), so a
/// single generic adapter serves every <see cref="IEntity"/> bound to a collection. Reads hit the
/// collection immediately; <see cref="Add"/>/<see cref="Update"/>/<see cref="Remove"/> are buffered
/// and flushed as one <c>BulkWrite</c> when the shared <see cref="MongoUnitOfWork"/> commits.
/// Scoped, so its buffer lives for exactly one request.
/// </summary>
public sealed class MongoRepository<T> : IRepository<T>, IUnitOfWorkParticipant
    where T : IEntity
{
    private readonly IMongoCollection<T> _collection;
    private readonly MongoUnitOfWork _unitOfWork;
    private readonly List<WriteModel<T>> _pending = [];

    public MongoRepository(IMongoCollection<T> collection, MongoUnitOfWork unitOfWork)
    {
        _collection = collection;
        _unitOfWork = unitOfWork;
    }

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        await _collection.Find(ById(id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken) =>
        await _collection.Find(FilterDefinition<T>.Empty).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate, CancellationToken cancellationToken) =>
        await _collection.Find(predicate).ToListAsync(cancellationToken);

    public void Add(T entity)
    {
        // Insert-or-replace by id: a manual create with a chosen GUID is idempotent on retry.
        _pending.Add(new ReplaceOneModel<T>(ById(entity.Id), entity) { IsUpsert = true });
        _unitOfWork.Enlist(this);
    }

    public void Update(T entity)
    {
        // Existence is checked by the use case before this is staged, so a plain replace suffices.
        _pending.Add(new ReplaceOneModel<T>(ById(entity.Id), entity));
        _unitOfWork.Enlist(this);
    }

    public void Remove(string id)
    {
        _pending.Add(new DeleteOneModel<T>(ById(id)));
        _unitOfWork.Enlist(this);
    }

    async Task IUnitOfWorkParticipant.FlushAsync(CancellationToken cancellationToken)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        await _collection.BulkWriteAsync(_pending, options: null, cancellationToken);
        _pending.Clear();
    }

    private static FilterDefinition<T> ById(string id) =>
        Builders<T>.Filter.Eq(entity => entity.Id, id);
}
