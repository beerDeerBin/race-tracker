import { describe, expect, it, vi } from 'vitest';
import { tokenStore } from './tokenStore';
import type { StoredAuth } from './tokenStore';

const auth: StoredAuth = {
    accessToken: 'token-123',
    expiresAt: '2099-01-01T00:00:00+00:00',
    username: 'admin',
};

describe('tokenStore', () => {
    it('returns the stored session and token after set', () => {
        tokenStore.set(auth);

        expect(tokenStore.get()).toEqual(auth);
        expect(tokenStore.getToken()).toBe('token-123');
    });

    it('mirrors the session to sessionStorage', () => {
        tokenStore.set(auth);

        expect(JSON.parse(sessionStorage.getItem('race-tracker.auth')!)).toEqual(auth);
    });

    it('clear removes the session from memory and storage', () => {
        tokenStore.set(auth);

        tokenStore.clear();

        expect(tokenStore.get()).toBeNull();
        expect(tokenStore.getToken()).toBeNull();
        expect(sessionStorage.getItem('race-tracker.auth')).toBeNull();
    });

    it('notifies subscribers on set and clear, and stops after unsubscribe', () => {
        const listener = vi.fn();
        const unsubscribe = tokenStore.subscribe(listener);

        tokenStore.set(auth);
        tokenStore.clear();
        unsubscribe();
        tokenStore.set(auth);

        expect(listener).toHaveBeenCalledTimes(2);
        expect(listener).toHaveBeenNthCalledWith(1, auth);
        expect(listener).toHaveBeenNthCalledWith(2, null);
    });
});
