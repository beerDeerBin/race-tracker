namespace RaceTracker.Realtime.Domain;

/// <summary>
/// Assembly marker for the Realtime domain layer. TP-RT is the live-push service
/// (Archetyp d, M6 part 1): it relays the shared <c>StatusEvent</c> contract to
/// SignalR clients and holds no domain entities of its own, so for the M6 scaffold
/// this layer only anchors the inward dependency direction (Application → Domain,
/// <c>/A20/</c>). The rules/event model arrives when this service is extended in M8.
/// </summary>
public static class RealtimeDomainMarker;
