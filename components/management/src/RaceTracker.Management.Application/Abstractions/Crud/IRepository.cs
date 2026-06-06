using System.Linq.Expressions;
using RaceTracker.Management.Domain.Abstractions;

namespace RaceTracker.Management.Application.Abstractions.Crud;

/// <summary>
/// Generic persistence port (<c>/A70/</c>) for any <see cref="IEntity"/>. The real adapter is
/// backed by MongoDB; unit tests mock it. Reads hit the store immediately; writes are
/// <b>buffered</b> and only applied when <see cref="IUnitOfWork.SaveChangesAsync"/> commits them —
/// so a use case can stage several mutations and persist them in one boundary (Repository +
/// Unit of Work, ARCHITECTURE_PRINCIPLES §6).
/// </summary>
public interface IRepository<T>
    where T : IEntity
{
    /// <summary>Returns the entity with the given id, or <c>null</c> if none exists.</summary>
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>Returns every entity of this type.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Returns the entities matching <paramref name="predicate"/>.</summary>
    Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);

    /// <summary>Stages an insert-or-replace of <paramref name="entity"/> (applied on commit).</summary>
    void Add(T entity);

    /// <summary>Stages an update of <paramref name="entity"/> (applied on commit).</summary>
    void Update(T entity);

    /// <summary>Stages a delete of the entity with id <paramref name="id"/> (applied on commit).</summary>
    void Remove(string id);
}
