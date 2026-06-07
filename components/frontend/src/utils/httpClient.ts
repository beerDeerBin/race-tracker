import axios from 'axios';
import type { AxiosResponse, InternalAxiosRequestConfig } from 'axios';
import { tokenStore } from './tokenStore';
import { logger } from './logger';

/**
 * The single configured HTTP instance (/U50/): every REST and GraphQL call flows through
 * it, so token injection, request/response logging and the central 401 → logout handling live
 * in exactly one place. Services build absolute URLs from `config` — the instance itself is
 * origin-agnostic because the SPA talks to three backend origins.
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

/**
 * Describes a request/response for the log without ever emitting a secret: only method, URL and
 * status, plus a masked flag for whether a bearer token was attached — never the token itself, and
 * never the request/response body (a login body carries the password). (§8 "log requests/responses,
 * masking secrets"; the logger contract requires callers to pass already-masked context.)
 */
function maskedRequest(request: InternalAxiosRequestConfig) {
    return {
        method: request.method?.toUpperCase(),
        url: request.url,
        authorization: request.headers.Authorization ? 'Bearer ***' : 'none',
    };
}

httpClient.interceptors.request.use((request) => {
    const token = tokenStore.getToken();
    if (token) {
        request.headers.Authorization = `Bearer ${token}`;
    }
    // Log after injection so the masked auth flag reflects what is actually sent.
    logger.info('HTTP request', maskedRequest(request));
    return request;
});

httpClient.interceptors.response.use(
    (response: AxiosResponse) => {
        logger.info('HTTP response', {
            method: response.config.method?.toUpperCase(),
            url: response.config.url,
            status: response.status,
        });
        return response;
    },
    (error: unknown) => {
        if (axios.isAxiosError(error)) {
            logger.warn('HTTP error', {
                method: error.config?.method?.toUpperCase(),
                url: error.config?.url,
                status: error.response?.status,
            });

            if (error.response?.status === 401 && !error.config?.skipUnauthorizedHandler) {
                // Central session-expiry path (/U50/): drop the dead token, let the app route to login.
                tokenStore.clear();
                unauthorizedHandler?.();
            }
        }
        return Promise.reject(error);
    },
);
