import { useCallback, useEffect, useMemo, useState } from 'react';
import type { PropsWithChildren } from 'react';
import { AuthContext } from './AuthContext';
import type { AuthContextValue } from './AuthContext';
import { authService } from '../services/authService';
import { registerUnauthorizedHandler } from '../utils/httpClient';
import { logger } from '../utils/logger';
import { tokenStore } from '../utils/tokenStore';

/**
 * Provides the auth context (/U10/): holds the signed-in user, performs login/logout, and
 * owns the central 401 reaction by registering the unauthorized handler on the single
 * HTTP instance (/U50/).
 */

function restoredUser(): string | null {
    const stored = tokenStore.get();
    if (!stored) {
        return null;
    }
    if (new Date(stored.expiresAt).getTime() <= Date.now()) {
        tokenStore.clear();
        return null;
    }
    return stored.username;
}

export function AuthProvider({ children }: PropsWithChildren) {
    const [user, setUser] = useState<string | null>(restoredUser);
    const [role, setRole] = useState<string | null>(null);

    const logout = useCallback(() => {
        tokenStore.clear();
        setUser(null);
        setRole(null);
    }, []);

    // Confirm the session against the real principal (/F11/): /me validates the token
    // server-side (a stale restored token 401s here and the central handler signs out)
    // and surfaces the role. A transient network failure keeps the session — the next
    // protected call settles it.
    useEffect(() => {
        if (!user) {
            return;
        }
        let cancelled = false;
        authService
            .me()
            .then((me) => {
                if (!cancelled) {
                    setRole(me.role);
                }
            })
            .catch((error: unknown) => {
                logger.warn('Could not confirm principal via /me', { error });
            });
        return () => {
            cancelled = true;
        };
    }, [user]);

    // Central 401 → logout (/U50/): the interceptor already cleared the store; reflect it in
    // state so the route guards redirect to login.
    useEffect(() => {
        registerUnauthorizedHandler(() => {
            setUser(null);
            setRole(null);
        });
        return () => registerUnauthorizedHandler(null);
    }, []);

    // Proactively end the session when the token expires, instead of waiting for a 401.
    useEffect(() => {
        if (!user) {
            return;
        }
        const expiresAt = tokenStore.get()?.expiresAt;
        if (!expiresAt) {
            return;
        }
        // Clamp into the 32-bit timer range — an overflowing delay would fire immediately
        // and sign the user out on the spot.
        const remainingMs = new Date(expiresAt).getTime() - Date.now();
        const timer = setTimeout(logout, Math.min(Math.max(remainingMs, 0), 2 ** 31 - 1));
        return () => clearTimeout(timer);
    }, [user, logout]);

    const login = useCallback(async (username: string, password: string) => {
        const response = await authService.login({ username, password });
        tokenStore.set({
            accessToken: response.accessToken,
            expiresAt: response.expiresAt,
            username,
        });
        setUser(username);
    }, []);

    const value = useMemo<AuthContextValue>(
        () => ({ user, role, isAuthenticated: user !== null, login, logout }),
        [user, role, login, logout],
    );

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
