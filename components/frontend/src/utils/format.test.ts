import { describe, expect, it } from 'vitest';
import { formatBattery, formatUptime, secondsSince } from './format';

describe('formatBattery', () => {
    it('formats known values with units', () => {
        expect(formatBattery(3987, 76)).toBe('3987 mV · 76 %');
    });

    it('renders the protocol sentinels as em dashes', () => {
        expect(formatBattery(65535, 76)).toBe('— · 76 %');
        expect(formatBattery(3987, 255)).toBe('3987 mV · —');
        expect(formatBattery(65535, 255)).toBe('— · —');
    });
});

describe('formatUptime', () => {
    it('formats seconds, minutes and hours compactly', () => {
        expect(formatUptime(47_000)).toBe('47s');
        expect(formatUptime(725_000)).toBe('12m 05s');
        expect(formatUptime(11_220_000)).toBe('3h 07m');
    });

    it('floors sub-second values', () => {
        expect(formatUptime(900)).toBe('0s');
    });
});

describe('secondsSince', () => {
    it('returns elapsed whole seconds', () => {
        const now = new Date('2026-06-07T12:00:10Z');
        expect(secondsSince('2026-06-07T12:00:00Z', now)).toBe(10);
    });

    it('clamps future timestamps (clock skew) to zero', () => {
        const now = new Date('2026-06-07T12:00:00Z');
        expect(secondsSince('2026-06-07T12:00:05Z', now)).toBe(0);
    });
});
