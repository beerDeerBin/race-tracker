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
     * 64-bit error bitmask (PROTOCOL §5.1), decoded to plaintext via utils/errorBitmask
     * (BigInt-safe). Typed as number: the highest defined bit is 42, well under 2^53, so
     * the value stays exact through JSON parsing.
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
