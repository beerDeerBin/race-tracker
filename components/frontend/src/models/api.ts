/**
 * Typed mirrors of the management REST contracts (defined once at their boundary in M5,
 * mirrored here — camelCase JSON exactly as the API serializes).
 */

export interface LoginRequest {
    username: string;
    password: string;
}

export interface LoginResponse {
    accessToken: string;
    tokenType: string;
    /** ISO 8601 timestamp. */
    expiresAt: string;
}

export interface MeResponse {
    username: string;
    role: string | null;
}

export type RegistrationStatus = 'pending' | 'registered';

/** IMU output data rate (wire values, PROTOCOL §4.2; underscore = decimal point). */
export type ImuOdr = 'hz12_5' | 'hz26' | 'hz52' | 'hz104' | 'hz208' | 'hz417' | 'hz833';

/** Accelerometer full-scale range in g. */
export type AccelRange = 'g2' | 'g4' | 'g8' | 'g16';

/** Gyroscope full-scale range in dps. */
export type GyroRange = 'dps125' | 'dps250' | 'dps500' | 'dps1000' | 'dps2000';

export interface StartRunRequest {
    /** Optional caller-chosen UUID; the server generates one when omitted. */
    runId?: string;
    /** Total samples to collect; must be at least 1. */
    numSamples: number;
    /** Defaults to "hz104" server-side when omitted. */
    odr?: ImuOdr;
    /** Defaults to "g4" server-side when omitted. */
    accelRange?: AccelRange;
    /** Defaults to "dps500" server-side when omitted. */
    gyroRange?: GyroRange;
}

export interface StartRunResponse {
    /** The effective run UUID (caller-chosen or server-generated). */
    runId: string;
}

export interface ClaimVehicleRequest {
    name: string;
    /** Optional — the server defaults to the authenticated username when omitted. */
    owner?: string;
}

export interface UpdateVehicleRequest {
    name: string;
    /** Optional — unchanged when omitted. */
    owner?: string;
    /** Optional — unchanged when omitted. */
    status?: RegistrationStatus;
}

export interface VehicleResponse {
    /** Opaque, case-sensitive cross-service correlation key — never re-case or re-parse. */
    deviceGuid: string;
    name: string;
    owner: string;
    registrationStatus: RegistrationStatus;
    /** ISO 8601 timestamp. */
    createdAt: string;
    metadata: Record<string, string>;
    /** Id of the gallery image shown as the vehicle's title/avatar, or null/undefined when none. */
    titleImageId?: string | null;
}

/** One gallery image's metadata (the binary is fetched separately from `/images/{id}`). */
export interface VehicleImageResponse {
    id: string;
    fileName: string;
    contentType: string;
    /** Size in bytes. */
    length: number;
    /** ISO 8601 timestamp. */
    uploadedAt: string;
}
