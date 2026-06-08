/**
 * Thin structured logging facade (§8 observability analogue for the SPA): one place to
 * change the sink later. Never log secrets — callers pass already-masked context.
 */
export const logger = {
    info(message: string, context?: unknown): void {
        console.info(`[race-tracker] ${message}`, context ?? '');
    },
    warn(message: string, context?: unknown): void {
        console.warn(`[race-tracker] ${message}`, context ?? '');
    },
    error(message: string, context?: unknown): void {
        console.error(`[race-tracker] ${message}`, context ?? '');
    },
};
