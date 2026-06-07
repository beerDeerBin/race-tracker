import { createContext } from 'react';

/**
 * Cross-cutting auth state (/U10/): consumed through the useAuth hook, provided by
 * AuthProvider (separate file so fast refresh sees a components-only module there).
 */
export interface AuthContextValue {
    user: string | null;
    /** Role from the confirmed principal (/me); null until confirmed. */
    role: string | null;
    isAuthenticated: boolean;
    /** Authenticates against the real management service; rejects on failure. */
    login(username: string, password: string): Promise<void>;
    logout(): void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
