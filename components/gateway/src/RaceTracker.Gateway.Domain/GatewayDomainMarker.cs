namespace RaceTracker.Gateway.Domain;

/// <summary>
/// Assembly marker for the Gateway domain layer. The gateway is a stateless edge
/// service (Archetyp b, <c>/L50/</c>): decoded telemetry models land here from
/// story 2.2 onward. For the 2.1 scaffold the layer exists only to anchor the
/// inward dependency direction (Application → Domain, <c>/A20/</c>).
/// </summary>
public static class GatewayDomainMarker;
