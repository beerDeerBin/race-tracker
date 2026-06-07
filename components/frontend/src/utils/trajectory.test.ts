import { describe, expect, it } from 'vitest';
import {
    boundsToViewBox,
    computeBounds,
    headingToSvgDegrees,
    pathD,
    pointAtTime,
    totalDuration,
} from './trajectory';
import type { TrajectoryPoint } from '../models/graphql';

function pt(index: number, t: number, x: number, y: number, heading = 0): TrajectoryPoint {
    return { index, t, x, y, heading };
}

describe('computeBounds', () => {
    it('returns the axis-aligned bounds of the path', () => {
        expect(computeBounds([pt(0, 0, 0, 0), pt(1, 1, 3, -2), pt(2, 2, -1, 5)])).toEqual({
            minX: -1,
            minY: -2,
            maxX: 3,
            maxY: 5,
        });
    });

    it('returns a zero box for an empty path', () => {
        expect(computeBounds([])).toEqual({ minX: 0, minY: 0, maxX: 0, maxY: 0 });
    });
});

describe('boundsToViewBox', () => {
    it('fits the bounds with padding, in y-flipped space', () => {
        // bounds 0..4 x, 0..2 y, padding 1 → x:-1 w:6 ; yTop: -(2)-1 = -3 h:4
        expect(boundsToViewBox({ minX: 0, minY: 0, maxX: 4, maxY: 2 }, 1)).toBe('-1 -3 6 4');
    });

    it('widens a degenerate (single-point/straight) axis to 1 m', () => {
        // a vertical line (spanX 0) → width 0+2 padding ; here check spanX floor
        expect(boundsToViewBox({ minX: 0, minY: 0, maxX: 0, maxY: 0 }, 1)).toBe('-1 -1 3 3');
    });
});

describe('pathD', () => {
    it('emits M/L commands in y-flipped space', () => {
        expect(pathD([pt(0, 0, 0, 0), pt(1, 1, 1, 2)])).toBe('M 0 0 L 1 -2');
    });

    it('is empty for no points', () => {
        expect(pathD([])).toBe('');
    });
});

describe('headingToSvgDegrees', () => {
    it('flips the math angle for SVG y-down space', () => {
        expect(headingToSvgDegrees(0)).toBeCloseTo(0);
        expect(headingToSvgDegrees(Math.PI / 2)).toBeCloseTo(-90);
        expect(headingToSvgDegrees(Math.PI)).toBeCloseTo(-180);
    });
});

describe('totalDuration', () => {
    it('is the last point time, 0 when empty', () => {
        expect(totalDuration([pt(0, 0, 0, 0), pt(1, 4.8, 1, 1)])).toBe(4.8);
        expect(totalDuration([])).toBe(0);
    });
});

describe('pointAtTime', () => {
    const points = [pt(0, 0, 0, 0), pt(1, 1, 1, 0), pt(2, 2, 2, 0)];

    it('clamps before start to the first point', () => {
        expect(pointAtTime(points, -5)?.index).toBe(0);
    });

    it('returns the last point with t <= time', () => {
        expect(pointAtTime(points, 1.5)?.index).toBe(1);
        expect(pointAtTime(points, 1)?.index).toBe(1);
    });

    it('clamps past the end to the last point', () => {
        expect(pointAtTime(points, 99)?.index).toBe(2);
    });

    it('is null for an empty path', () => {
        expect(pointAtTime([], 0)).toBeNull();
    });
});
