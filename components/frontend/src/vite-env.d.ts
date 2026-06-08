/// <reference types="vite/client" />

interface ImportMetaEnv {
    readonly VITE_MANAGEMENT_URL?: string;
    readonly VITE_PERSISTENCE_URL?: string;
    readonly VITE_REALTIME_URL?: string;
}

interface ImportMeta {
    readonly env: ImportMetaEnv;
}
