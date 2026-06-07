import { afterEach, describe, expect, it, vi } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import type { InternalAxiosRequestConfig } from 'axios';
import { httpClient, registerUnauthorizedHandler } from './httpClient';
import { tokenStore } from './tokenStore';

function okAdapter(captured: { config?: InternalAxiosRequestConfig }) {
    return async (requestConfig: InternalAxiosRequestConfig) => {
        captured.config = requestConfig;
        return { data: {}, status: 200, statusText: 'OK', headers: {}, config: requestConfig };
    };
}

function failingAdapter(status: number) {
    return async (requestConfig: InternalAxiosRequestConfig) => {
        throw new AxiosError('request failed', undefined, requestConfig, undefined, {
            data: {},
            status,
            statusText: 'error',
            headers: {},
            config: requestConfig,
        });
    };
}

describe('httpClient interceptors', () => {
    afterEach(() => {
        registerUnauthorizedHandler(null);
        httpClient.defaults.adapter = undefined;
    });

    it('injects the bearer token when a session exists', async () => {
        tokenStore.set({
            accessToken: 'token-abc',
            expiresAt: '2099-01-01T00:00:00Z',
            username: 'admin',
        });
        const captured: { config?: InternalAxiosRequestConfig } = {};
        httpClient.defaults.adapter = okAdapter(captured);

        await httpClient.get('http://backend/protected');

        expect(AxiosHeaders.from(captured.config!.headers).Authorization).toBe('Bearer token-abc');
    });

    it('sends no Authorization header without a session', async () => {
        const captured: { config?: InternalAxiosRequestConfig } = {};
        httpClient.defaults.adapter = okAdapter(captured);

        await httpClient.get('http://backend/open');

        expect(AxiosHeaders.from(captured.config!.headers).Authorization).toBeUndefined();
    });

    it('clears the session and invokes the unauthorized handler on 401', async () => {
        tokenStore.set({
            accessToken: 'stale',
            expiresAt: '2099-01-01T00:00:00Z',
            username: 'admin',
        });
        const onUnauthorized = vi.fn();
        registerUnauthorizedHandler(onUnauthorized);
        httpClient.defaults.adapter = failingAdapter(401);

        await expect(httpClient.get('http://backend/protected')).rejects.toBeInstanceOf(AxiosError);

        expect(tokenStore.get()).toBeNull();
        expect(onUnauthorized).toHaveBeenCalledTimes(1);
    });

    it('skips the unauthorized handler when the request opts out (login)', async () => {
        const onUnauthorized = vi.fn();
        registerUnauthorizedHandler(onUnauthorized);
        httpClient.defaults.adapter = failingAdapter(401);

        await expect(
            httpClient.post('http://backend/login', {}, { skipUnauthorizedHandler: true }),
        ).rejects.toBeInstanceOf(AxiosError);

        expect(onUnauthorized).not.toHaveBeenCalled();
    });

    it('leaves the session untouched on non-401 errors', async () => {
        tokenStore.set({
            accessToken: 'live',
            expiresAt: '2099-01-01T00:00:00Z',
            username: 'admin',
        });
        const onUnauthorized = vi.fn();
        registerUnauthorizedHandler(onUnauthorized);
        httpClient.defaults.adapter = failingAdapter(500);

        await expect(httpClient.get('http://backend/broken')).rejects.toBeInstanceOf(AxiosError);

        expect(tokenStore.getToken()).toBe('live');
        expect(onUnauthorized).not.toHaveBeenCalled();
    });
});
