import { describe, expect, it } from 'vitest';
import en from './en.json';
import de from './de.json';

/** Collects every leaf key path (dot-joined) of a nested translation object. */
function keyPaths(obj: Record<string, unknown>, prefix = ''): string[] {
    return Object.entries(obj).flatMap(([key, value]) => {
        const path = prefix ? `${prefix}.${key}` : key;
        return value !== null && typeof value === 'object'
            ? keyPaths(value as Record<string, unknown>, path)
            : [path];
    });
}

describe('i18n bundle parity (/U40/)', () => {
    it('en and de expose the identical key set', () => {
        const enKeys = keyPaths(en).sort();
        const deKeys = keyPaths(de).sort();

        const missingInDe = enKeys.filter((k) => !deKeys.includes(k));
        const missingInEn = deKeys.filter((k) => !enKeys.includes(k));

        expect(missingInDe, 'keys present in en but missing in de').toEqual([]);
        expect(missingInEn, 'keys present in de but missing in en').toEqual([]);
    });
});
