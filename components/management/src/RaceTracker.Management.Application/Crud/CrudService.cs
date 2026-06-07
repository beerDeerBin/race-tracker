using RaceTracker.Management.Application.Abstractions.Crud;
using RaceTracker.Management.Application.Observability;
using RaceTracker.Management.Domain.Abstractions;

namespace RaceTracker.Management.Application.Crud;

/// <summary>
/// Generic CRUD use case (<c>/A70/</c>, ARCHITECTURE_PRINCIPLES §6) over any <see cref="IEntity"/>:
/// the standard list/get/create/update/delete flows, implemented <b>once</b> and bound by concrete
/// entities (Vehicle is the first, story 5.3). Each mutation stages its change through the
/// <see cref="IRepository{T}"/> and then commits the single <see cref="IUnitOfWork"/> boundary —
/// the same instance, so the staged write is flushed exactly once. Left non-sealed with
/// <c>virtual</c> methods so a discriminator variant can specialise it when an entity hierarchy
/// first appears, without changing this surface.
/// </summary>
public class CrudService<T>
    where T : IEntity
{
    private static readonly string _entityName = typeof(T).Name;

    private readonly IRepository<T> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ManagementMetrics _metrics;

    public CrudService(IRepository<T> repository, IUnitOfWork unitOfWork, ManagementMetrics metrics)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _metrics = metrics;
    }

    /// <summary>Returns every entity.</summary>
    public virtual Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken) =>
        _repository.GetAllAsync(cancellationToken);

    /// <summary>Returns the entity by id, or <c>null</c> if it does not exist.</summary>
    public virtual Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        _repository.GetByIdAsync(id, cancellationToken);

    /// <summary>
    /// Stages and commits an insert of <paramref name="entity"/>. Returns <c>false</c> (without
    /// committing) when an entity with the same id already exists, so the caller can answer 409 —
    /// a create never silently overwrites an existing record.
    /// </summary>
    public virtual async Task<bool> CreateAsync(T entity, CancellationToken cancellationToken)
    {
        T? existing = await _repository.GetByIdAsync(entity.Id, cancellationToken);
        if (existing is not null)
        {
            return false;
        }

        _repository.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _metrics.RecordCrud(_entityName, "create");
        return true;
    }

    /// <summary>
    /// Reads the entity with id <paramref name="id"/>, applies <paramref name="apply"/> to it, then
    /// stages and commits the update — a single read shared with the existence check. Returns the
    /// updated entity, or <c>null</c> (without committing) when no such entity exists, so the caller
    /// can answer 404.
    /// </summary>
    public virtual async Task<T?> UpdateAsync(
        string id, Func<T, T> apply, CancellationToken cancellationToken)
    {
        T? existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return default;
        }

        T updated = apply(existing);
        _repository.Update(updated);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _metrics.RecordCrud(_entityName, "update");
        return updated;
    }

    /// <summary>
    /// Stages and commits a delete of the entity with id <paramref name="id"/>. Returns <c>false</c>
    /// (without committing) when no such entity exists, so the caller can answer 404.
    /// </summary>
    public virtual async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        T? existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        _repository.Remove(id);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _metrics.RecordCrud(_entityName, "delete");
        return true;
    }
}
