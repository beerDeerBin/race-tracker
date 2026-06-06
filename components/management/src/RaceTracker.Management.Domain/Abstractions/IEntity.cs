namespace RaceTracker.Management.Domain.Abstractions;

/// <summary>
/// The shared contract every CRUD entity satisfies: a stable string identifier that doubles as
/// the document's <c>_id</c>. Introduced with the first CRUD entity (story 5.3) and reused by the
/// generic CRUD building blocks (<c>/A70/</c>, Leitprinzip <i>einmalig festgezurrt</i>) — the
/// <c>Repository&lt;T&gt;</c> / <c>CrudService&lt;T&gt;</c> / <c>Controller&lt;T&gt;</c> stack is
/// constrained to <c>where T : IEntity</c>. Plain abstraction with zero framework dependencies.
/// </summary>
public interface IEntity
{
    /// <summary>Stable identifier (maps to the store's primary key / Mongo <c>_id</c>).</summary>
    string Id { get; }
}
