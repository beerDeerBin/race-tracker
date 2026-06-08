import { timeOfIndex } from './odr';
import type { Sample, SampleRollupBucket } from '../models/graphql';

/**
 * Pure mapping from samples to uPlot's aligned-data layout: one shared x array (time in
 * seconds from ODR) plus one y array per axis. Kept free of uPlot/DOM so it unit-tests
 * without a canvas.
 */

export type AlignedData = [number[], ...number[][]];

export function toAccelData(samples: readonly Sample[], odrHz: number): AlignedData {
    return [
        samples.map((sample) => timeOfIndex(sample.index, odrHz)),
        samples.map((sample) => sample.ax),
        samples.map((sample) => sample.ay),
        samples.map((sample) => sample.az),
    ];
}

export function toGyroData(samples: readonly Sample[], odrHz: number): AlignedData {
    return [
        samples.map((sample) => timeOfIndex(sample.index, odrHz)),
        samples.map((sample) => sample.gx),
        samples.map((sample) => sample.gy),
        samples.map((sample) => sample.gz),
    ];
}

/**
 * Aggregate-view layout (/F53/): per axis three columns — min, max, avg — so the chart
 * can band min↔max and draw avg on top. Column order: t, xMin, xMax, xAvg, yMin, … .
 */
function toRollupData(
    buckets: readonly SampleRollupBucket[],
    odrHz: number,
    axes: ['ax' | 'gx', 'ay' | 'gy', 'az' | 'gz'],
): AlignedData {
    const t = buckets.map((bucket) => timeOfIndex(bucket.bucketStartIndex, odrHz));
    const columns = axes.flatMap((axis) => [
        buckets.map((bucket) => bucket[axis].min),
        buckets.map((bucket) => bucket[axis].max),
        buckets.map((bucket) => bucket[axis].avg),
    ]);
    return [t, ...columns];
}

export function toAccelRollupData(
    buckets: readonly SampleRollupBucket[],
    odrHz: number,
): AlignedData {
    return toRollupData(buckets, odrHz, ['ax', 'ay', 'az']);
}

export function toGyroRollupData(
    buckets: readonly SampleRollupBucket[],
    odrHz: number,
): AlignedData {
    return toRollupData(buckets, odrHz, ['gx', 'gy', 'gz']);
}

/** Inclusive time-range filter on raw samples (/F82/); open-ended bounds via null. */
export function filterSamplesByTime<T extends { index: number }>(
    samples: readonly T[],
    odrHz: number,
    fromSeconds: number | null,
    toSeconds: number | null,
): T[] {
    return samples.filter((sample) => {
        const t = timeOfIndex(sample.index, odrHz);
        return (fromSeconds === null || t >= fromSeconds) && (toSeconds === null || t <= toSeconds);
    });
}

/** Inclusive time-range filter on roll-up buckets, keyed by the bucket's start time. */
export function filterBucketsByTime(
    buckets: readonly SampleRollupBucket[],
    odrHz: number,
    fromSeconds: number | null,
    toSeconds: number | null,
): SampleRollupBucket[] {
    return buckets.filter((bucket) => {
        const t = timeOfIndex(bucket.bucketStartIndex, odrHz);
        return (fromSeconds === null || t >= fromSeconds) && (toSeconds === null || t <= toSeconds);
    });
}
