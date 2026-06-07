/**
 * Typed mirrors of the realtime SignalR push contracts (defined once in M6, mirrored here —
 * camelCase JSON exactly as the hub serializes).
 */

export type DeviceState = 'Idle' | 'Connected' | 'Acquiring';

/** Pushed as the "DeviceStatus" hub event to the per-guid group (/F60/, /D30/). */
export interface DeviceStatusUpdate {
    /** Opaque, case-sensitive cross-service correlation key. */
    deviceGuid: string;
    state: DeviceState;
    uptimeMs: number;
    /** Battery voltage in mV; 65535 = unknown. */
    batteryMv: number;
    /** Battery charge 0–100; 255 = unknown. */
    batteryPct: number;
    /**
     * 64-bit error bitmask (PROTOCOL §5.1). JSON numbers lose precision above 2^53, so this
     * field is only safe for zero/nonzero checks until the BigInt-based decoder lands in 7.8.
     */
    errorCode: number;
    /** ISO 8601 timestamp of the gateway observation — used for newest-wins merging. */
    observedAtUtc: string;
}

/** Pushed as the "RunProgress" hub event to the per-guid group while a run is active (/F61/). */
export interface RunProgressUpdate {
    /** Opaque, case-sensitive cross-service correlation key. */
    deviceGuid: string;
    /** Samples collected so far in the current run. */
    sampledCount: number;
    /** Samples requested for the current run. */
    totalSamples: number;
    /** ISO 8601 timestamp of the gateway observation — used for newest-wins merging. */
    observedAtUtc: string;
}
