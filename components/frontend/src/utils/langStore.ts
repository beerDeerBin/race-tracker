/**
 * Language preference store (/U40/), persisted to localStorage — a UI preference, like the
 * theme. The only reader/writer of its storage key.
 */
const STORAGE_KEY = 'race-tracker.lang';

export const LANGUAGES = ['en', 'de'] as const;
export type Language = (typeof LANGUAGES)[number];

function isLanguage(value: unknown): value is Language {
    return (LANGUAGES as readonly unknown[]).includes(value);
}

export const langStore = {
    /** Stored preference, or null when none/garbage. */
    get(): Language | null {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            return isLanguage(raw) ? raw : null;
        } catch {
            return null;
        }
    },

    set(language: Language): void {
        try {
            localStorage.setItem(STORAGE_KEY, language);
        } catch {
            // Storage unavailable — i18n still switches in memory.
        }
    },
};

/** Resolves the initial language: stored preference → browser → English. */
export function initialLanguage(): Language {
    const stored = langStore.get();
    if (stored) {
        return stored;
    }
    if (typeof navigator !== 'undefined' && navigator.language.startsWith('de')) {
        return 'de';
    }
    return 'en';
}
