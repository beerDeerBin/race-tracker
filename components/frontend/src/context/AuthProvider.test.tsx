import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { PropsWithChildren } from 'react';
import { AuthProvider } from './AuthProvider';
import { useAuth } from '../hooks/useAuth';
import { authService } from '../services/authService';
import { tokenStore } from '../utils/tokenStore';
import * as httpClientModule from '../utils/httpClient';

vi.mock('../services/authService', () => ({
    authService: {
        login: vi.fn(),
        me: vi.fn(),
    },
}));

const loginMock = vi.mocked(authService.login);
const meMock = vi.mocked(authService.me);

beforeEach(() => {
    // The provider confirms every session via /me; default to a happy principal.
    meMock.mockReset();
    meMock.mockResolvedValue({ username: 'admin', role: 'admin' });
});

function wrapper({ children }: PropsWithChildren) {
    return <AuthProvider>{children}</AuthProvider>;
}

describe('AuthContext', () => {
    it('starts unauthenticated without a stored session', () => {
        const { result } = renderHook(() => useAuth(), { wrapper });

        expect(result.current.isAuthenticated).toBe(false);
        expect(result.current.user).toBeNull();
    });

    it('restores an unexpired stored session and confirms it via /me', async () => {
        tokenStore.set({
            accessToken: 'live',
            expiresAt: '2099-01-01T00:00:00+00:00',
            username: 'admin',
        });

        const { result } = renderHook(() => useAuth(), { wrapper });

        expect(result.current.isAuthenticated).toBe(true);
        expect(result.current.user).toBe('admin');
        await waitFor(() => expect(result.current.role).toBe('admin'));
        expect(meMock).toHaveBeenCalled();
    });

    it('keeps the session when /me fails transiently', async () => {
        meMock.mockReset();
        meMock.mockRejectedValue(new Error('network down'));
        tokenStore.set({
            accessToken: 'live',
            expiresAt: '2099-01-01T00:00:00+00:00',
            username: 'admin',
        });

        const { result } = renderHook(() => useAuth(), { wrapper });

        await waitFor(() => expect(meMock).toHaveBeenCalled());
        expect(result.current.isAuthenticated).toBe(true);
        expect(result.current.role).toBeNull();
    });

    it('discards an expired stored session', () => {
        tokenStore.set({
            accessToken: 'stale',
            expiresAt: '2000-01-01T00:00:00+00:00',
            username: 'admin',
        });

        const { result } = renderHook(() => useAuth(), { wrapper });

        expect(result.current.isAuthenticated).toBe(false);
        expect(tokenStore.get()).toBeNull();
    });

    it('login stores the session and authenticates', async () => {
        loginMock.mockResolvedValueOnce({
            accessToken: 'jwt',
            tokenType: 'Bearer',
            expiresAt: '2099-01-01T00:00:00+00:00',
        });
        const { result } = renderHook(() => useAuth(), { wrapper });

        await act(() => result.current.login('admin', 'secret'));

        expect(loginMock).toHaveBeenCalledWith({ username: 'admin', password: 'secret' });
        expect(result.current.isAuthenticated).toBe(true);
        expect(result.current.user).toBe('admin');
        expect(tokenStore.getToken()).toBe('jwt');
    });

    it('login failure rejects and stays unauthenticated', async () => {
        loginMock.mockRejectedValueOnce(new Error('401'));
        const { result } = renderHook(() => useAuth(), { wrapper });

        await expect(act(() => result.current.login('admin', 'wrong'))).rejects.toThrow();

        expect(result.current.isAuthenticated).toBe(false);
        expect(tokenStore.get()).toBeNull();
    });

    it('logout clears the session', async () => {
        tokenStore.set({
            accessToken: 'live',
            expiresAt: '2099-01-01T00:00:00+00:00',
            username: 'admin',
        });
        const { result } = renderHook(() => useAuth(), { wrapper });

        act(() => result.current.logout());

        expect(result.current.isAuthenticated).toBe(false);
        expect(tokenStore.get()).toBeNull();
    });

    it('registers a 401 handler that signs the user out', async () => {
        const registerSpy = vi.spyOn(httpClientModule, 'registerUnauthorizedHandler');
        tokenStore.set({
            accessToken: 'live',
            expiresAt: '2099-01-01T00:00:00+00:00',
            username: 'admin',
        });
        const { result } = renderHook(() => useAuth(), { wrapper });
        expect(result.current.isAuthenticated).toBe(true);

        // Simulate the interceptor's 401 path: session already cleared, then the handler fires.
        const handler = registerSpy.mock.calls.at(-1)?.[0];
        expect(handler).toBeTypeOf('function');
        tokenStore.clear();
        act(() => handler!());

        await waitFor(() => expect(result.current.isAuthenticated).toBe(false));
    });
});
