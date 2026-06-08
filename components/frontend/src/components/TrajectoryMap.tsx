import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Flag, Info } from 'lucide-react';
import {
    boundsToViewBox,
    computeBounds,
    pathD,
    tangentSvgDegrees,
    trajectoryStats,
} from '../utils/trajectory';
import type { TrajectoryPoint } from '../models/graphql';

/** Rounds a raw spacing to a "nice" 1/2/5 × 10ⁿ value for the reference grid. */
function niceStep(raw: number): number {
    if (raw <= 0) {
        return 1;
    }
    const pow = Math.pow(10, Math.floor(Math.log10(raw)));
    const f = raw / pow;
    const n = f >= 5 ? 5 : f >= 2 ? 2 : 1;
    return n * pow;
}

/**
 * The 2D track map (/F80/): a custom SVG (local meters, not geo). Draws the dead-reckoned path
 * (heading-integrated, so it curves wherever the run turned), a reference grid, the start (0,0)
 * and a checkered finish, and a heading-oriented vehicle marker at the active point. The portion
 * already driven (up to the playback point) is highlighted in F1 red so progress is visible.
 * Pure presentational — all geometry comes from the 4.3 points + utils/trajectory.
 *
 * The viewBox is padded by the marker extent so the start/finish/vehicle markers can't clip at the
 * box edges (they're drawn at the path's extreme points).
 */
export function TrajectoryMap({
    points,
    active,
}: {
    points: TrajectoryPoint[];
    active: TrajectoryPoint | null;
}) {
    const { t } = useTranslation();
    const [statsOpen, setStatsOpen] = useState(false);

    const bounds = useMemo(() => computeBounds(points), [points]);
    const spanX = Math.max(bounds.maxX - bounds.minX, 1);
    const spanY = Math.max(bounds.maxY - bounds.minY, 1);
    const span = Math.max(spanX, spanY);

    const stroke = span / 180;
    const markerR = span / 30;
    // Pad the viewBox by the marker reach (+ stroke) so markers at the extreme points never clip.
    const padding = markerR * 1.8 + stroke;

    const viewBox = useMemo(() => boundsToViewBox(bounds, padding), [bounds, padding]);
    const gridStep = niceStep(span / 6);

    const fullD = useMemo(() => pathD(points), [points]);
    const activeIndex = active ? points.indexOf(active) : -1;
    const traveledD = useMemo(
        () => (activeIndex > 0 ? pathD(points.slice(0, activeIndex + 1)) : ''),
        [points, activeIndex],
    );
    const last = points.length > 0 ? points[points.length - 1]! : null;
    const isStraight = points.length > 2 && Math.min(spanX, spanY) < 0.06 * Math.max(spanX, spanY);

    const stats = useMemo(() => trajectoryStats(points), [points]);
    const statRows: [string, string][] = [
        [t('trajectory.topSpeed'), `${stats.topSpeedMps.toFixed(1)} m/s`],
        [t('trajectory.avgSpeed'), `${stats.avgSpeedMps.toFixed(1)} m/s`],
        [t('trajectory.peakAccel'), `${stats.peakAccelMps2.toFixed(1)} m/s²`],
        [t('trajectory.distance'), `${stats.distanceM.toFixed(1)} m`],
        [t('trajectory.duration'), `${stats.durationS.toFixed(1)} s`],
        [t('trajectory.across'), `${Math.round(span)} m`],
    ];
    const statList = (className: string) => (
        <dl className={className}>
            {statRows.map(([label, value]) => (
                <div key={label} className="flex justify-between gap-3">
                    <dt className="text-slate-500 dark:text-slate-400">{label}</dt>
                    <dd className="font-mono tabular-nums">{value}</dd>
                </div>
            ))}
        </dl>
    );

    return (
        <div className="card relative p-4">
            {isStraight && (
                <p className="mb-2 text-xs text-slate-500 dark:text-slate-400">
                    {t('trajectory.straightHint')}
                </p>
            )}
            <svg
                viewBox={viewBox}
                role="img"
                aria-label={t('trajectory.mapLabel')}
                preserveAspectRatio="xMidYMid meet"
                className="h-[420px] w-full"
            >
                <defs>
                    <pattern
                        id="rt-map-grid"
                        width={gridStep}
                        height={gridStep}
                        patternUnits="userSpaceOnUse"
                    >
                        <path
                            d={`M ${gridStep} 0 L 0 0 0 ${gridStep}`}
                            fill="none"
                            className="text-slate-300 dark:text-slate-700"
                            stroke="currentColor"
                            strokeWidth={stroke * 0.4}
                        />
                    </pattern>
                    <pattern
                        id="rt-map-finish"
                        width={markerR / 1.5}
                        height={markerR / 1.5}
                        patternUnits="userSpaceOnUse"
                    >
                        <rect width={markerR / 1.5} height={markerR / 1.5} fill="#f8fafc" />
                        <rect width={markerR / 3} height={markerR / 3} fill="#0f172a" />
                        <rect
                            x={markerR / 3}
                            y={markerR / 3}
                            width={markerR / 3}
                            height={markerR / 3}
                            fill="#0f172a"
                        />
                    </pattern>
                </defs>

                {/* Reference grid. */}
                <rect
                    x={bounds.minX - padding}
                    y={-bounds.maxY - padding}
                    width={spanX + padding * 2}
                    height={spanY + padding * 2}
                    fill="url(#rt-map-grid)"
                    opacity="0.6"
                />

                {/* Track: a soft glow + the base grey line. */}
                <path
                    d={fullD}
                    fill="none"
                    className="text-slate-400 dark:text-slate-600"
                    stroke="currentColor"
                    strokeOpacity="0.45"
                    strokeWidth={stroke * 2.6}
                    strokeLinejoin="round"
                    strokeLinecap="round"
                />
                <path
                    data-testid="track-path"
                    d={fullD}
                    fill="none"
                    className="text-slate-300 dark:text-slate-600"
                    stroke="currentColor"
                    strokeWidth={stroke}
                    strokeLinejoin="round"
                    strokeLinecap="round"
                />

                {/* Driven portion (up to the playback point), in F1 red. */}
                {traveledD && (
                    <path
                        d={traveledD}
                        fill="none"
                        stroke="#e10600"
                        strokeWidth={stroke * 1.7}
                        strokeLinejoin="round"
                        strokeLinecap="round"
                    />
                )}

                {/* Start marker at (0,0). */}
                <circle
                    data-testid="start-marker"
                    cx={0}
                    cy={0}
                    r={markerR}
                    fill="#22c55e"
                    stroke="#f8fafc"
                    strokeWidth={stroke}
                />

                {/* Checkered finish marker at the last point. */}
                {last && points.length > 1 && (
                    <rect
                        x={last.x - markerR}
                        y={-last.y - markerR}
                        width={markerR * 2}
                        height={markerR * 2}
                        fill="url(#rt-map-finish)"
                        stroke="#0f172a"
                        strokeWidth={stroke * 0.5}
                        opacity="0.9"
                    />
                )}

                {/* Vehicle marker: a triangle pointing along +x, rotated to the path tangent so it
                    follows the track (not the raw device heading, which drifts off the path). */}
                {active && (
                    <g
                        transform={`translate(${active.x} ${-active.y}) rotate(${tangentSvgDegrees(points, activeIndex)})`}
                    >
                        <polygon
                            points={`${markerR * 1.6},0 ${-markerR},${markerR} ${-markerR},${-markerR}`}
                            fill="#e10600"
                            stroke="#f8fafc"
                            strokeWidth={stroke * 0.6}
                        />
                    </g>
                )}
            </svg>

            {/* Telemetry HUD (bottom-right): inline stats panel on md+, an Info-button popover below. */}
            <div className="absolute right-3 bottom-3 z-10">
                {statList(
                    'hidden w-44 flex-col gap-0.5 rounded-md border border-slate-200/70 bg-white/85 px-3 py-2 text-xs shadow-sm backdrop-blur md:flex dark:border-slate-700/70 dark:bg-slate-950/80',
                )}
                <div className="relative md:hidden">
                    <button
                        type="button"
                        onClick={() => setStatsOpen((open) => !open)}
                        aria-expanded={statsOpen}
                        aria-label={t('trajectory.stats')}
                        className="inline-flex items-center justify-center rounded-full border border-slate-300 bg-white/90 p-1.5 text-slate-600 shadow-sm backdrop-blur transition-colors hover:text-f1-red dark:border-slate-700 dark:bg-slate-950/80 dark:text-slate-300"
                    >
                        <Info className="h-4 w-4" aria-hidden="true" />
                    </button>
                    {statsOpen &&
                        statList(
                            'absolute right-0 bottom-full mb-2 flex w-44 flex-col gap-0.5 rounded-md border border-slate-200 bg-white px-3 py-2 text-xs shadow-lg dark:border-slate-700 dark:bg-slate-900',
                        )}
                </div>
            </div>

            {/* Legend. */}
            <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-slate-500 dark:text-slate-400">
                <span className="inline-flex items-center gap-1.5">
                    <span className="inline-block h-2.5 w-2.5 rounded-full bg-emerald-500" />
                    {t('trajectory.start')}
                </span>
                <span className="inline-flex items-center gap-1.5">
                    <span className="inline-block h-2.5 w-2.5 rounded-full bg-f1-red" />
                    {t('trajectory.car')}
                </span>
                <span className="inline-flex items-center gap-1.5">
                    <Flag className="h-3.5 w-3.5" aria-hidden="true" />
                    {t('trajectory.finish')}
                </span>
            </div>
        </div>
    );
}
