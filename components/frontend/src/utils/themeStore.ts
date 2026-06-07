/**
 * Theme preference store: 'light' | 'dark' | 'system', persisted to localStorage (a UI
 * preference, not a secret — intentionally outlives the tab, unlike the auth session).
 * Mirrors the tokenStore pattern: the only reader/writer of its storage key.
 */
const STORAGE_KEY = 'race-tracker.theme';

export const THEMES = ['light', 'dark', 'system'] as const;
export type Theme = (typeof THEMES)[number];

function isTheme(value: unknown): value is Theme {
    return (THEMES as readonly unknown[]).includes(value);
}

export const themeStore = {
    get(): Theme {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            return isTheme(raw) ? raw : 'system';
        } catch {
            return 'system';
        }
    },

    set(theme: Theme): void {
        try {
            localStorage.setItem(STORAGE_KEY, theme);
        } catch {
            // Storage unavailable — the in-memory state still drives the UI.
        }
    },
};

/** Resolves 'system' against the OS preference; pure helper for testability. */
export function resolveTheme(theme: Theme, systemPrefersDark: boolean): 'light' | 'dark' {
    if (theme === 'system') {
        return systemPrefersDark ? 'dark' : 'light';
    }
    return theme;
}
