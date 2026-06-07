import axios from 'axios';
import { httpClient } from '../utils/httpClient';
import { config } from '../utils/config';
import { encodeGuid } from '../utils/encodeGuid';
import { DeviceNotFoundError } from './vehicleService';
import type { StartRunRequest, StartRunResponse } from '../models/api';

/**
 * Device command resource of the management service (/F30/–/F33/): fire-and-forget
 * dispatch over MQTT — every call answers 202; outcomes surface via the live status
 * (the device sends no NACK, /F35/).
 */

function commandUrl(deviceGuid: string, command: string): string {
    return `${config.managementUrl}/vehicles/${encodeGuid(deviceGuid)}/commands/${command}`;
}

async function post<TResponse = void>(
    deviceGuid: string,
    command: string,
    body?: unknown,
): Promise<TResponse> {
    try {
        const { data } = await httpClient.post<TResponse>(commandUrl(deviceGuid, command), body);
        return data;
    } catch (error) {
        if (axios.isAxiosError(error) && error.response?.status === 404) {
            throw new DeviceNotFoundError(deviceGuid);
        }
        throw error;
    }
}

export const commandService = {
    /** IDLE → CONNECTED. */
    connect: (deviceGuid: string) => post(deviceGuid, 'connect'),

    /** CONNECTED → ACQUIRING; resolves the effective runId. */
    startRun: (deviceGuid: string, request: StartRunRequest) =>
        post<StartRunResponse>(deviceGuid, 'start-run', request),

    /** CONNECTED → IDLE. */
    disconnect: (deviceGuid: string) => post(deviceGuid, 'disconnect'),

    /** IDLE/CONNECTED → IDLE, clears uptime + error codes. */
    reset: (deviceGuid: string) => post(deviceGuid, 'reset'),
};
