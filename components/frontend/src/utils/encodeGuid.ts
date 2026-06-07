/**
 * Encodes a device guid for use as a URL path segment. The guid is the cross-service
 * correlation key and is **case-sensitive** (CONVENTIONS §8) — it must never be
 * lowercased or round-tripped through a Guid type; this is the only transformation
 * allowed on it.
 */
export function encodeGuid(deviceGuid: string): string {
    return encodeURIComponent(deviceGuid);
}
