import { beforeEach, describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import type { PropsWithChildren } from 'react';
import { ThemeProvider } from './ThemeProvider';
import { useTheme } from '../hooks/useTheme';

function wrapper({ children }: PropsWithChildren) {
    return <ThemeProvider>{children}</ThemeProvider>;
}

describe('ThemeProvider', () => {
    beforeEach(() => {
        localStorage.clear();
        document.documentElement.classList.remove('dark');
    });

    it('defaults to the system preference (light in jsdom)', () => {
        const { result } = renderHook(() => useTheme(), { wrapper });

        expect(result.current.theme).toBe('system');
        expect(result.current.resolvedTheme).toBe('light');
        expect(document.documentElement.classList.contains('dark')).toBe(false);
    });

    it('setTheme(dark) applies the dark class and persists the choice', () => {
        const { result } = renderHook(() => useTheme(), { wrapper });

        act(() => result.current.setTheme('dark'));

        expect(result.current.resolvedTheme).toBe('dark');
        expect(document.documentElement.classList.contains('dark')).toBe(true);
        expect(localStorage.getItem('race-tracker.theme')).toBe('dark');
    });

    it('setTheme(light) removes the dark class', () => {
        const { result } = renderHook(() => useTheme(), { wrapper });

        act(() => result.current.setTheme('dark'));
        act(() => result.current.setTheme('light'));

        expect(result.current.resolvedTheme).toBe('light');
        expect(document.documentElement.classList.contains('dark')).toBe(false);
    });

    it('restores a persisted preference on mount', () => {
        localStorage.setItem('race-tracker.theme', 'dark');

        const { result } = renderHook(() => useTheme(), { wrapper });

        expect(result.current.theme).toBe('dark');
        expect(document.documentElement.classList.contains('dark')).toBe(true);
    });
});
