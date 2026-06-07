namespace RaceTracker.Management.Domain;

/// <summary>
/// Assembly marker for the Management domain layer (TP-MGMT, Archetyp a: system of
/// record). For the 5.1 grundgerüst this layer only anchors the inward dependency
/// direction (Application → Domain, <c>/A20/</c>); the <c>User</c> entity arrives with
/// auth (story 5.2) and <c>Vehicle</c> with the CRUD + generic base classes (story 5.3).
/// </summary>
public static class ManagementDomainMarker;
