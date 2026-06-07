namespace RaceTracker.Persistence.Domain;

/// <summary>
/// Assembly marker for the Persistence domain layer. TP-PERS is the time-series
/// write+read store (Archetyp c): the persisted schema is owned as SQL migrations
/// (story 3.1), so for the 3.2 scaffold this layer only anchors the inward
/// dependency direction (Application → Domain, <c>/A20/</c>). The <c>Run</c> and
/// <c>Sample</c> entities + repository arrive with the upsert path in story 3.3.
/// </summary>
public static class PersistenceDomainMarker;
