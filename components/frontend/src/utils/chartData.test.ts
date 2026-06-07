import { describe, expect, it } from 'vitest';
import { toAccelData, toGyroData } from './chartData';
import type { Sample } from '../models/graphql';

const samples: Sample[] = [
    { index: 0, ax: 1, ay: 2, az: 3, gx: 4, gy: 5, gz: 6 },
    { index: 52, ax: 10, ay: 20, az: 30, gx: 40, gy: 50, gz: 60 },
];

describe('chartData', () => {
    it('aligns acceleration series on the ODR-derived time axis', () => {
        const [t, ax, ay, az] = toAccelData(samples, 104);

        expect(t).toEqual([0, 0.5]);
        expect(ax).toEqual([1, 10]);
        expect(ay).toEqual([2, 20]);
        expect(az).toEqual([3, 30]);
    });

    it('aligns gyro series the same way', () => {
        const [t, gx, gy, gz] = toGyroData(samples, 104);

        expect(t).toEqual([0, 0.5]);
        expect(gx).toEqual([4, 40]);
        expect(gy).toEqual([5, 50]);
        expect(gz).toEqual([6, 60]);
    });

    it('handles empty input', () => {
        expect(toAccelData([], 104)).toEqual([[], [], [], []]);
    });
});
