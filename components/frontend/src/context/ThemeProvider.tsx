import { useCallback, useEffect, useMemo, useState } from 'react';
import type { PropsWithChildren } from 'react';
import { ThemeContext } from './ThemeContext';
import type { ThemeContextValue } from './ThemeContext';
import { resolveTheme, themeStore } from '../utils/themeStore';
import type { Theme } from '../utils/themeStore';

/**
 * Provides the color scheme: light / dark / follow-the-OS. The resolved scheme is applied
 * as a `dark` class on <html> (Tailwind's class-based dark variant); 'system' tracks the
 * OS preference live via the media query.
 */

const DARK_QUERY = '(prefers-color-scheme: dark)';

function systemPrefersDark(): boolean {
    return typeof window.matchMedia === 'function' && window.matchMedia(DARK_QUERY).matches;
}

export function ThemeProvider({ children }: PropsWithChildren) {
    const [theme, setThemeState] = useState<Theme>(themeStore.get);
    const [prefersDark, setPrefersDark] = useState(systemPrefersDark);

    // Track the OS preference so 'system' switches live without a reload.
    useEffect(() => {
        if (typeof window.matchMedia !== 'function') {
            return;
        }
        const query = window.matchMedia(DARK_QUERY);
        const onChange = (event: MediaQueryListEvent) => setPrefersDark(event.matches);
        query.addEventListener('change', onChange);
        return () => query.removeEventListener('change', onChange);
    }, []);

    const resolvedTheme = resolveTheme(theme, prefersDark);

    // Reflect the resolved scheme on <html> for Tailwind's `dark:` variant.
    useEffect(() => {
        document.documentElement.classList.toggle('dark', resolvedTheme === 'dark');
    }, [resolvedTheme]);

    const setTheme = useCallback((next: Theme) => {
        themeStore.set(next);
        setThemeState(next);
    }, []);

    const value = useMemo<ThemeContextValue>(
        () => ({ theme, resolvedTheme, setTheme }),
        [theme, resolvedTheme, setTheme],
    );

    return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}
