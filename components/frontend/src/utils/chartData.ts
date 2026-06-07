import { timeOfIndex } from './odr';
import type { Sample } from '../models/graphql';

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
