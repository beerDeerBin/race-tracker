import { describe, expect, it } from 'vitest';
import { encodeGuid } from './encodeGuid';

describe('encodeGuid', () => {
    it('preserves the case of the guid verbatim', () => {
        expect(encodeGuid('AABB-ccDD-0099')).toBe('AABB-ccDD-0099');
    });

    it('escapes URL-special characters', () => {
        expect(encodeGuid('a/b?c#d')).toBe('a%2Fb%3Fc%23d');
    });
});
