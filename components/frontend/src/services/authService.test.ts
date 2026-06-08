import { describe, expect, it, vi } from 'vitest';
import { AxiosError } from 'axios';
import { authService, InvalidCredentialsError } from './authService';
import { httpClient } from '../utils/httpClient';
import { config } from '../utils/config';

describe('authService', () => {
    it('login posts the credentials to the management /login route and opts out of the 401 handler', async () => {
        const response = {
            accessToken: 'jwt',
            tokenType: 'Bearer',
            expiresAt: '2099-01-01T00:00:00+00:00',
        };
        const post = vi.spyOn(httpClient, 'post').mockResolvedValueOnce({ data: response });

        const result = await authService.login({ username: 'admin', password: 'secret' });

        expect(post).toHaveBeenCalledWith(
            `${config.managementUrl}/login`,
            { username: 'admin', password: 'secret' },
            { skipUnauthorizedHandler: true },
        );
        expect(result).toEqual(response);
    });

    it('login maps a 401 to InvalidCredentialsError', async () => {
        const unauthorized = new AxiosError('Unauthorized');
        unauthorized.response = { status: 401 } as AxiosError['response'];
        vi.spyOn(httpClient, 'post').mockRejectedValueOnce(unauthorized);

        await expect(authService.login({ username: 'admin', password: 'wrong' })).rejects.toThrow(
            InvalidCredentialsError,
        );
    });

    it('login rethrows non-401 failures untouched', async () => {
        const serverError = new Error('boom');
        vi.spyOn(httpClient, 'post').mockRejectedValueOnce(serverError);

        await expect(authService.login({ username: 'admin', password: 'x' })).rejects.toBe(
            serverError,
        );
    });

    it('me reads the authenticated principal from the management /me route', async () => {
        const response = { username: 'admin', role: 'admin' };
        const get = vi.spyOn(httpClient, 'get').mockResolvedValueOnce({ data: response });

        const result = await authService.me();

        expect(get).toHaveBeenCalledWith(`${config.managementUrl}/me`);
        expect(result).toEqual(response);
    });
});
