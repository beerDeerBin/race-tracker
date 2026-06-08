import { useEffect, useRef } from 'react';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';
import { useTheme } from '../hooks/useTheme';
import type { AlignedData } from '../utils/chartData';

/**
 * The one uPlot wrapper (/F81/, /F82/): canvas-rendered time series, smooth at 8000+
 * points. The instance is created once per mount/theme and fed via setData; width tracks
 * the container through a ResizeObserver. Aggregate views (7.6) add min/max bands and
 * per-series visibility — same wrapper, no second chart path.
 */

export interface AxisChartSeries {
    label: string;
    color: string;
    /** Stroke width in px (default 1). */
    width?: number;
    /** Dash pattern, e.g. [4, 4] for the faint min/max outlines. */
    dash?: number[];
}

/** Translucent fill between two series (1-based series indices, as uPlot counts them). */
export interface AxisChartBand {
    from: number;
    to: number;
    fill: string;
}

// NOTE for callers: pass `series` and `bands` as stable references (module-level
// constants) — they are dependencies of the create-effect, so inline arrays would
// silently destroy and recreate the canvas on every render.

const HEIGHT = 280;

const LIGHT = { axis: '#475569', grid: '#e2e8f0' };
const DARK = { axis: '#94a3b8', grid: '#334155' };

export function AxisChart({
    title,
    unit,
    series,
    data,
    bands,
    visibility,
}: {
    title: string;
    /** Y-axis unit label, e.g. "m/s²" or "rad/s". */
    unit: string;
    series: AxisChartSeries[];
    data: AlignedData;
    bands?: AxisChartBand[];
    /** Per-series show flags (same order as `series`); omitted = all visible. */
    visibility?: boolean[];
}) {
    const containerRef = useRef<HTMLDivElement>(null);
    const plotRef = useRef<uPlot | null>(null);
    // Latest visibility for the create-effect: a theme-driven recreation must not reset
    // the user's axis filter. Synced in an effect (declared first, so it runs before the
    // create-effect of the same commit).
    const visibilityRef = useRef(visibility);
    useEffect(() => {
        visibilityRef.current = visibility;
    }, [visibility]);
    const { resolvedTheme } = useTheme();

    // Create the instance once per mount/theme; uPlot axis colors are constructor-time.
    useEffect(() => {
        const container = containerRef.current;
        if (!container) {
            return;
        }
        const colors = resolvedTheme === 'dark' ? DARK : LIGHT;

        const options: uPlot.Options = {
            width: container.clientWidth || 600,
            height: HEIGHT,
            scales: { x: { time: false } },
            series: [
                { label: 't [s]' },
                ...series.map((s) => ({
                    label: s.label,
                    stroke: s.color,
                    width: s.width ?? 1,
                    dash: s.dash,
                })),
            ],
            bands: bands?.map((band) => ({
                series: [band.from, band.to] as [number, number],
                fill: band.fill,
            })),
            axes: [
                {
                    label: 't [s]',
                    stroke: colors.axis,
                    grid: { stroke: colors.grid, width: 1 },
                    ticks: { stroke: colors.grid },
                },
                {
                    label: unit,
                    stroke: colors.axis,
                    grid: { stroke: colors.grid, width: 1 },
                    ticks: { stroke: colors.grid },
                },
            ],
        };

        const plot = new uPlot(options, data as uPlot.AlignedData, container);
        plotRef.current = plot;
        visibilityRef.current?.forEach((show, seriesIndex) => {
            plot.setSeries(seriesIndex + 1, { show });
        });

        const observer = new ResizeObserver(() => {
            plot.setSize({ width: container.clientWidth || 600, height: HEIGHT });
        });
        observer.observe(container);

        return () => {
            observer.disconnect();
            plotRef.current = null;
            plot.destroy();
        };
        // `data`/`visibility` are intentionally NOT dependencies — they flow through
        // setData/setSeries below without recreating the canvas.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [resolvedTheme, unit, series, bands]);

    // Feed data changes into the existing instance.
    useEffect(() => {
        plotRef.current?.setData(data as uPlot.AlignedData);
    }, [data]);

    // Toggle series visibility in place (/F82/ axis filter).
    useEffect(() => {
        const plot = plotRef.current;
        if (!plot || !visibility) {
            return;
        }
        visibility.forEach((show, seriesIndex) => {
            plot.setSeries(seriesIndex + 1, { show });
        });
    }, [visibility]);

    return (
        <section className="rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
            <h3 className="mb-2 text-sm font-medium text-slate-700 dark:text-slate-300">{title}</h3>
            <div ref={containerRef} />
        </section>
    );
}
