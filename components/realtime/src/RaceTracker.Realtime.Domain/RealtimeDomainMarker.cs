namespace RaceTracker.Realtime.Domain;

/// <summary>
/// Assembly marker for the Realtime domain layer. TP-RT is the live-push service
/// (Archetyp d, M6 part 1): it relays the shared <c>StatusEvent</c> contract to
/// SignalR clients and holds no domain entities of its own, so this layer only
/// anchors the inward dependency direction (Application → Domain, <c>/A20/</c>). The
/// M8 rule engine (story 8.1) lives in Application (<c>Rules/</c>) — pure orchestration
/// over the shared contract, not domain entities — so Domain stays a marker.
/// </summary>
public static class RealtimeDomainMarker;
