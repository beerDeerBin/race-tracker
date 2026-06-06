namespace RaceTracker.Management.Application.Abstractions;

/// <summary>
/// Port for probing the document store's reachability (backs the readiness check, /A80/).
/// The real adapter pings MongoDB; throws if the store is unreachable.
/// </summary>
public interface IMongoConnectivityCheck
{
    Task CheckAsync(CancellationToken cancellationToken);
}
