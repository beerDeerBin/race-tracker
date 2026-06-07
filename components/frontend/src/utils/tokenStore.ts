/**
 * Single source of truth for the auth session (/U50/): in-memory value mirrored to
 * sessionStorage (survives a reload within the tab, gone when the tab closes — acceptable
 * for the 60-minute JWT and avoids an XSS-persistent localStorage token). Nothing else
 * reads or writes storage; the HTTP client and the SignalR client (7.2) both pull the
 * token from here.
 */
const STORAGE_KEY = 'race-tracker.auth';

export interface StoredAuth {
    accessToken: string;
    /** ISO timestamp from the login response; used to expire the session client-side. */
    expiresAt: string;
    username: string;
}

type Listener = (auth: StoredAuth | null) => void;

function readFromStorage(): StoredAuth | null {
    try {
        const raw = sessionStorage.getItem(STORAGE_KEY);
        return raw ? (JSON.parse(raw) as StoredAuth) : null;
    } catch {
        return null;
    }
}

let current: StoredAuth | null = readFromStorage();
const listeners = new Set<Listener>();

function notify(): void {
    for (const listener of listeners) {
        listener(current);
    }
}

export const tokenStore = {
    get(): StoredAuth | null {
        return current;
    },

    getToken(): string | null {
        return current?.accessToken ?? null;
    },

    set(auth: StoredAuth): void {
        current = auth;
        try {
            sessionStorage.setItem(STORAGE_KEY, JSON.stringify(auth));
        } catch {
            // Storage unavailable (private mode quota etc.) — the in-memory session still works.
        }
        notify();
    },

    clear(): void {
        current = null;
        try {
            sessionStorage.removeItem(STORAGE_KEY);
        } catch {
            // Ignore — nothing stored.
        }
        notify();
    },

    /** Subscribe to session changes; returns the unsubscribe function. */
    subscribe(listener: Listener): () => void {
        listeners.add(listener);
        return () => listeners.delete(listener);
    },
};
