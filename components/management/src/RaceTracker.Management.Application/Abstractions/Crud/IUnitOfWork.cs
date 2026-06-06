namespace RaceTracker.Management.Application.Abstractions.Crud;

/// <summary>
/// The atomic-write boundary (<c>/A70/</c>, ARCHITECTURE_PRINCIPLES §6). Groups the buffered
/// mutations staged through <see cref="IRepository{T}"/> and owns the single commit, so a use case
/// can mutate several aggregates and persist them in one <see cref="SaveChangesAsync"/> call. The
/// real adapter is backed by MongoDB; unit tests mock it.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Commits all staged writes and clears the buffer.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
