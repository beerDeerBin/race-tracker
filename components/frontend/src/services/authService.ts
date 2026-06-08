import axios from 'axios';
import { httpClient } from '../utils/httpClient';
import { config } from '../utils/config';
import type { LoginRequest, LoginResponse, MeResponse } from '../models/api';

/** Thrown on a login 401 so callers never need to know the transport's error shape. */
export class InvalidCredentialsError extends Error {
    constructor() {
        super('Invalid username or password');
        this.name = 'InvalidCredentialsError';
    }
}

/**
 * Auth resource of the management service (services layer: the only place that knows the
 * /login and /me routes; components never call the network directly).
 */
export const authService = {
    async login(request: LoginRequest): Promise<LoginResponse> {
        try {
            // A 401 here means bad credentials, not an expired session — skip the central logout.
            const { data } = await httpClient.post<LoginResponse>(
                `${config.managementUrl}/login`,
                request,
                {
                    skipUnauthorizedHandler: true,
                },
            );
            return data;
        } catch (error) {
            if (axios.isAxiosError(error) && error.response?.status === 401) {
                throw new InvalidCredentialsError();
            }
            throw error;
        }
    },

    async me(): Promise<MeResponse> {
        const { data } = await httpClient.get<MeResponse>(`${config.managementUrl}/me`);
        return data;
    },
};
