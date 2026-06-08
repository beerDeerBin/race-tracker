import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../hooks/useTheme';
import { boundsToViewBox, computeBounds, headingToSvgDegrees, pathD } from '../utils/trajectory';
import type { TrajectoryPoint } from '../models/graphql';

/**
 * The 2D track map (/F80/): a custom SVG (local meters, not geo). Draws the path, marks
 * the start (0,0), and places a heading-oriented vehicle marker at the active point.
 * Pure presentational — all geometry comes from the 4.3 points + utils/trajectory.
 */
export function TrajectoryMap({
    points,
    active,
}: {
    points: TrajectoryPoint[];
    active: TrajectoryPoint | null;
}) {
    const { t } = useTranslation();
    const { resolvedTheme } = useTheme();

    const viewBox = useMemo(() => boundsToViewBox(computeBounds(points)), [points]);
    const d = useMemo(() => pathD(points), [points]);

    // Stroke width in user units (meters) scaled to the viewbox so it stays visible.
    const span = Math.max(
        ...viewBox
            .split(' ')
            .slice(2)
            .map((n) => Number(n)),
        1,
    );
    const stroke = span / 200;
    const markerR = span / 60;

    const pathColor = resolvedTheme === 'dark' ? '#38bdf8' : '#0284c7';
    const gridColor = resolvedTheme === 'dark' ? '#334155' : '#cbd5e1';

    return (
        <div className="rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
            <svg
                viewBox={viewBox}
                role="img"
                aria-label={t('trajectory.mapLabel')}
                preserveAspectRatio="xMidYMid meet"
                className="h-[420px] w-full"
            >
                {/* Path */}
                <path d={d} fill="none" stroke={pathColor} strokeWidth={stroke} />

                {/* Start marker at (0,0) → (0,0) in flipped space too. */}
                <circle
                    cx={0}
                    cy={0}
                    r={markerR}
                    fill="#22c55e"
                    stroke={gridColor}
                    strokeWidth={stroke / 2}
                />

                {/* Vehicle marker: a triangle pointing along +x, rotated by heading. */}
                {active && (
                    <g
                        transform={`translate(${active.x} ${-active.y}) rotate(${headingToSvgDegrees(active.heading)})`}
                    >
                        <polygon
                            points={`${markerR * 1.6},0 ${-markerR},${markerR} ${-markerR},${-markerR}`}
                            fill="#ef4444"
                        />
                    </g>
                )}
            </svg>
        </div>
    );
}
