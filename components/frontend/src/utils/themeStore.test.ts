import { beforeEach, describe, expect, it } from 'vitest';
import { resolveTheme, themeStore } from './themeStore';

describe('themeStore', () => {
    beforeEach(() => {
        localStorage.clear();
    });

    it('defaults to system without a stored preference', () => {
        expect(themeStore.get()).toBe('system');
    });

    it('round-trips a stored preference', () => {
        themeStore.set('dark');

        expect(themeStore.get()).toBe('dark');
        expect(localStorage.getItem('race-tracker.theme')).toBe('dark');
    });

    it('falls back to system on a garbage stored value', () => {
        localStorage.setItem('race-tracker.theme', 'neon');

        expect(themeStore.get()).toBe('system');
    });
});

describe('resolveTheme', () => {
    it('passes explicit choices through', () => {
        expect(resolveTheme('light', true)).toBe('light');
        expect(resolveTheme('dark', false)).toBe('dark');
    });

    it('resolves system against the OS preference', () => {
        expect(resolveTheme('system', true)).toBe('dark');
        expect(resolveTheme('system', false)).toBe('light');
    });
});
