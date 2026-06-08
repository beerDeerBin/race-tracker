import { describe, expect, it } from 'vitest';
import { DEFAULT_ODR_HZ, effectiveOdrHz, timeOfIndex } from './odr';

describe('effectiveOdrHz', () => {
    it('uses the run metadata when present', () => {
        expect(effectiveOdrHz({ odrHz: 208 })).toBe(208);
    });

    it('falls back to the backend default for missing or invalid metadata', () => {
        expect(effectiveOdrHz({ odrHz: null })).toBe(DEFAULT_ODR_HZ);
        expect(effectiveOdrHz({ odrHz: 0 })).toBe(DEFAULT_ODR_HZ);
        expect(effectiveOdrHz(null)).toBe(DEFAULT_ODR_HZ);
        expect(effectiveOdrHz(undefined)).toBe(DEFAULT_ODR_HZ);
    });
});

describe('timeOfIndex', () => {
    it('derives t = index / odrHz', () => {
        expect(timeOfIndex(0, 104)).toBe(0);
        expect(timeOfIndex(104, 104)).toBe(1);
        expect(timeOfIndex(52, 104)).toBeCloseTo(0.5);
    });
});
