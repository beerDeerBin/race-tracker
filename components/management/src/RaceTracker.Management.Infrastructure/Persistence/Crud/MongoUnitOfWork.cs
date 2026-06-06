using RaceTracker.Management.Application.Abstractions.Crud;

namespace RaceTracker.Management.Infrastructure.Persistence.Crud;

/// <summary>
/// A participant in a <see cref="MongoUnitOfWork"/> commit — typically a
/// <see cref="MongoRepository{T}"/> that has buffered writes for one collection and flushes them as
/// a single bulk write when the unit of work commits.
/// </summary>
internal interface IUnitOfWorkParticipant
{
    /// <summary>Flushes this participant's buffered writes; a no-op when it has none.</summary>
    Task FlushAsync(CancellationToken cancellationToken);
}

/// <summary>
/// MongoDB unit of work (<c>/A70/</c>): the single atomic-write boundary for one request scope.
/// Repositories <see cref="Enlist"/> themselves when they buffer a write; <see cref="SaveChangesAsync"/>
/// asks each enlisted participant to flush its buffer (one bulk write per collection). Because the
/// dev/test Mongo is a standalone node (no multi-document transactions), this batches and commits the
/// staged writes rather than wrapping them in a transaction — sufficient for the single-entity CRUD
/// flows here, where each request stages exactly one write. Scoped, so each request gets a fresh,
/// empty boundary that is shared with the request's repositories.
/// </summary>
public sealed class MongoUnitOfWork : IUnitOfWork
{
    private readonly List<IUnitOfWorkParticipant> _participants = [];

    internal void Enlist(IUnitOfWorkParticipant participant)
    {
        if (!_participants.Contains(participant))
        {
            _participants.Add(participant);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (IUnitOfWorkParticipant participant in _participants)
        {
            await participant.FlushAsync(cancellationToken);
        }
    }
}
