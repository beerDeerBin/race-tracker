import axios from 'axios';
import { tokenStore } from './tokenStore';

/**
 * The single configured HTTP instance (/U50/): every REST and GraphQL call flows through
 * it, so token injection and the central 401 → logout handling live in exactly one place.
 * Services build absolute URLs from `config` — the instance itself is origin-agnostic
 * because the SPA talks to three backend origins.
 */

declare module 'axios' {
    // Augmentation must repeat the original type parameter (incl. default) to merge.
    // eslint-disable-next-line @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars
    export interface AxiosRequestConfig<D = any> {
        /**
         * Opt out of the central 401 → logout handling for calls where a 401 is a domain
         * answer rather than an expired session (the login request itself).
         */
        skipUnauthorizedHandler?: boolean;
    }
}

type UnauthorizedHandler = () => void;

let unauthorizedHandler: UnauthorizedHandler | null = null;

/** Registered once by the auth context; invoked after a 401 cleared the session. */
export function registerUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
    unauthorizedHandler = handler;
}

export const httpClient = axios.create();

httpClient.interceptors.request.use((request) => {
    const token = tokenStore.getToken();
    if (token) {
        request.headers.Authorization = `Bearer ${token}`;
    }
    return request;
});

httpClient.interceptors.response.use(
    (response) => response,
    (error: unknown) => {
        if (
            axios.isAxiosError(error) &&
            error.response?.status === 401 &&
            !error.config?.skipUnauthorizedHandler
        ) {
            // Central session-expiry path (/U50/): drop the dead token, let the app route to login.
            tokenStore.clear();
            unauthorizedHandler?.();
        }
        return Promise.reject(error);
    },
);
