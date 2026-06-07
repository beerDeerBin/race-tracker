import { describe, expect, it } from 'vitest';
import { decodeErrorBits, ERROR_BITS } from './errorBitmask';

describe('decodeErrorBits', () => {
    it('returns no keys for a clean status', () => {
        expect(decodeErrorBits(0)).toEqual([]);
    });

    it('maps each defined bit to its key', () => {
        for (const { bit, key } of ERROR_BITS) {
            expect(decodeErrorBits(2 ** bit)).toEqual([key]);
        }
    });

    it('decodes a combined mask in table order', () => {
        // bits 9 (wifiConnect) + 34 (imuRead) + 42 (pwrBatteryCritical)
        const mask = 2 ** 9 + 2 ** 34 + 2 ** 42;
        expect(decodeErrorBits(mask)).toEqual([
            'errors.wifiConnect',
            'errors.imuRead',
            'errors.pwrBatteryCritical',
        ]);
    });

    it('handles the highest bit safely (battery critical, bit 42)', () => {
        expect(decodeErrorBits(2 ** 42)).toEqual(['errors.pwrBatteryCritical']);
    });

    it('accepts bigint and numeric-string input', () => {
        expect(decodeErrorBits(1n << 42n)).toEqual(['errors.pwrBatteryCritical']);
        expect(decodeErrorBits(String(2 ** 32))).toEqual(['errors.imuInit']);
    });

    it('ignores unmapped (reserved) bits', () => {
        // bits 3–7 are reserved gaps; only the mapped bit 0 should surface.
        const mask = 2 ** 0 + 2 ** 3 + 2 ** 7;
        expect(decodeErrorBits(mask)).toEqual(['errors.eepromParameter']);
    });

    it('returns nothing for garbage input', () => {
        expect(decodeErrorBits('not-a-number')).toEqual([]);
    });
});
