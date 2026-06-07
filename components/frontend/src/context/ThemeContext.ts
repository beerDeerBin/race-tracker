import { createContext } from 'react';
import type { Theme } from '../utils/themeStore';

/** Cross-cutting theme state; consumed via the useTheme hook, provided by ThemeProvider. */
export interface ThemeContextValue {
    /** The user's preference (may be 'system'). */
    theme: Theme;
    /** The effective scheme after resolving 'system' against the OS. */
    resolvedTheme: 'light' | 'dark';
    setTheme(theme: Theme): void;
}

export const ThemeContext = createContext<ThemeContextValue | null>(null);
