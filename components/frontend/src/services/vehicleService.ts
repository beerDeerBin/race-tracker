import axios from 'axios';
import { httpClient } from '../utils/httpClient';
import { config } from '../utils/config';
import { encodeGuid } from '../utils/encodeGuid';
import type { ClaimVehicleRequest, UpdateVehicleRequest, VehicleResponse } from '../models/api';

/** Thrown when the targeted device guid is unknown (404) — keeps transport out of the UI. */
export class DeviceNotFoundError extends Error {
    constructor(deviceGuid: string) {
        super(`Device ${deviceGuid} is not known`);
        this.name = 'DeviceNotFoundError';
    }
}

/**
 * Vehicle resource of the management service (/F83/, /F25/): the only place that knows
 * the /vehicles routes. The list includes discovery-created pending vehicles.
 */
export const vehicleService = {
    async list(): Promise<VehicleResponse[]> {
        const { data } = await httpClient.get<VehicleResponse[]>(
            `${config.managementUrl}/vehicles`,
        );
        return data;
    },

    /** Claims a pending device: sets name + owner, status flips to "registered". */
    async claim(deviceGuid: string, request: ClaimVehicleRequest): Promise<VehicleResponse> {
        try {
            const { data } = await httpClient.post<VehicleResponse>(
                `${config.managementUrl}/vehicles/${encodeGuid(deviceGuid)}/claim`,
                request,
            );
            return data;
        } catch (error) {
            if (axios.isAxiosError(error) && error.response?.status === 404) {
                throw new DeviceNotFoundError(deviceGuid);
            }
            throw error;
        }
    },

    /** Updates a vehicle (e.g. rename); unchanged fields may be omitted. */
    async update(deviceGuid: string, request: UpdateVehicleRequest): Promise<VehicleResponse> {
        const { data } = await httpClient.put<VehicleResponse>(
            `${config.managementUrl}/vehicles/${encodeGuid(deviceGuid)}`,
            request,
        );
        return data;
    },

    /** Releases a claimed vehicle: clears owner, resets the name to the guid, status → "pending". */
    async unclaim(deviceGuid: string): Promise<VehicleResponse> {
        const { data } = await httpClient.post<VehicleResponse>(
            `${config.managementUrl}/vehicles/${encodeGuid(deviceGuid)}/unclaim`,
        );
        return data;
    },
};
