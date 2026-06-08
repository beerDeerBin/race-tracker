/**
 * Central typed access to the build-time environment — the only place that reads
 * `import.meta.env`, so backend origins are configured once (Options-pattern analogue).
 * Defaults match the local Tilt stack.
 */
export const config = {
    managementUrl: import.meta.env.VITE_MANAGEMENT_URL ?? 'http://localhost:8083',
    persistenceUrl: import.meta.env.VITE_PERSISTENCE_URL ?? 'http://localhost:8082',
    realtimeUrl: import.meta.env.VITE_REALTIME_URL ?? 'http://localhost:8084',
} as const;
