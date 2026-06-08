import { describe, expect, it } from 'vitest';
import {
    filterBucketsByTime,
    filterSamplesByTime,
    toAccelData,
    toAccelRollupData,
    toGyroData,
    toGyroRollupData,
} from './chartData';
import type { Sample, SampleRollupBucket } from '../models/graphql';

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

function bucket(bucketStartIndex: number, base: number): SampleRollupBucket {
    const axis = (offset: number) => ({
        min: base + offset,
        max: base + offset + 1,
        avg: base + offset + 0.5,
    });
    return {
        bucketStartIndex,
        ax: axis(0),
        ay: axis(10),
        az: axis(20),
        gx: axis(30),
        gy: axis(40),
        gz: axis(50),
        sampleCount: 100,
    };
}

describe('rollup chart data', () => {
    it('lays out per-axis min/max/avg columns over the bucket time axis', () => {
        const data = toAccelRollupData([bucket(0, 0), bucket(100, 100)], 104);

        expect(data).toHaveLength(10);
        expect(data[0]).toEqual([0, 100 / 104]); // t from bucketStartIndex
        expect(data[1]).toEqual([0, 100]); // ax min
        expect(data[2]).toEqual([1, 101]); // ax max
        expect(data[3]).toEqual([0.5, 100.5]); // ax avg
        expect(data[4]).toEqual([10, 110]); // ay min
        expect(data[9]).toEqual([20.5, 120.5]); // az avg
    });

    it('maps the gyro triple from the gx/gy/gz rollups', () => {
        const data = toGyroRollupData([bucket(0, 0)], 104);

        expect(data[1]).toEqual([30]); // gx min
        expect(data[9]).toEqual([50.5]); // gz avg
    });
});

describe('time-range filters', () => {
    const samples: Sample[] = [0, 52, 104, 208].map((index) => ({
        index,
        ax: 0,
        ay: 0,
        az: 0,
        gx: 0,
        gy: 0,
        gz: 0,
    }));

    it('keeps samples inside the inclusive bounds', () => {
        const filtered = filterSamplesByTime(samples, 104, 0.5, 1);

        expect(filtered.map((s) => s.index)).toEqual([52, 104]);
    });

    it('treats null bounds as open-ended', () => {
        expect(filterSamplesByTime(samples, 104, null, null)).toHaveLength(4);
        expect(filterSamplesByTime(samples, 104, 1, null).map((s) => s.index)).toEqual([104, 208]);
        expect(filterSamplesByTime(samples, 104, null, 0).map((s) => s.index)).toEqual([0]);
    });

    it('filters buckets by their start time', () => {
        const buckets = [bucket(0, 0), bucket(100, 1), bucket(200, 2)];

        const filtered = filterBucketsByTime(buckets, 104, 0.5, 1.5);

        expect(filtered.map((b) => b.bucketStartIndex)).toEqual([100]);
    });
});
