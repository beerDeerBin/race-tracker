import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { ThemeProvider } from '../context/ThemeProvider';
import { TrajectoryMap } from './TrajectoryMap';
import { tangentSvgDegrees } from '../utils/trajectory';
import type { TrajectoryPoint } from '../models/graphql';

const points: TrajectoryPoint[] = [
    { index: 0, t: 0, x: 0, y: 0, heading: 0 },
    { index: 1, t: 1, x: 2, y: 1, heading: Math.PI / 2 },
];

function renderMap(active: TrajectoryPoint | null) {
    return render(
        <ThemeProvider>
            <TrajectoryMap points={points} active={active} />
        </ThemeProvider>,
    );
}

describe('TrajectoryMap', () => {
    it('draws the path and a start marker at (0,0)', () => {
        const { container } = renderMap(null);

        const path = container.querySelector('[data-testid="track-path"]');
        expect(path?.getAttribute('d')).toBe('M 0 0 L 2 -1');
        const start = container.querySelector('[data-testid="start-marker"]');
        expect(start?.getAttribute('cx')).toBe('0');
        expect(start?.getAttribute('cy')).toBe('0');
    });

    it('places the tangent-oriented vehicle marker at the active point (y-flipped)', () => {
        const { container } = renderMap(points[1]!);

        const marker = container.querySelector('polygon')?.parentElement;
        // x=2,y=1 → translate(2 -1); marker follows the path tangent (not the raw heading).
        const expected = `translate(2 -1) rotate(${tangentSvgDegrees(points, 1)})`;
        expect(marker?.getAttribute('transform')).toBe(expected);
    });

    it('omits the vehicle marker when there is no active point', () => {
        const { container } = renderMap(null);
        expect(container.querySelector('polygon')).toBeNull();
    });
});
