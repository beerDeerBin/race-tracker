/**
 * Typed mirrors of the persistence GraphQL read API (M4 contracts, camelCase exactly as
 * served; verified against the live schema).
 */

export interface Run {
    deviceGuid: string;
    runId: string;
    /** Requested sample count — null until a producer supplies run metadata. */
    numSamples: number | null;
    /** Acquisition rate in Hz — null today (see utils/odr.ts fallback). */
    odrHz: number | null;
    accelRange: number | null;
    gyroRange: number | null;
    /** ISO 8601 timestamps. */
    startedAt: string | null;
    endedAt: string | null;
    /** Samples actually ingested. */
    receivedSamples: number;
}

export interface Sample {
    /** Absolute run sample index; time derives as t = index / odrHz. */
    index: number;
    ax: number;
    ay: number;
    az: number;
    gx: number;
    gy: number;
    gz: number;
}
