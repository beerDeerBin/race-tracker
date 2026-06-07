import { useEffect, useRef } from 'react';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';
import { useTheme } from '../hooks/useTheme';
import type { AlignedData } from '../utils/chartData';

/**
 * The one uPlot wrapper (/F81/): canvas-rendered time series, smooth at 8000+ points ×
 * 3 series. The instance is created once per mount/theme and fed via setData; width
 * tracks the container through a ResizeObserver.
 */

export interface AxisChartSeries {
    label: string;
    color: string;
}

// NOTE for callers: pass `series` as a stable reference (module-level constant) — it is a
// dependency of the create-effect, so an inline array would silently destroy and recreate
// the canvas on every render.

const HEIGHT = 280;

const LIGHT = { axis: '#475569', grid: '#e2e8f0' };
const DARK = { axis: '#94a3b8', grid: '#334155' };

export function AxisChart({
    title,
    unit,
    series,
    data,
}: {
    title: string;
    /** Y-axis unit label, e.g. "m/s²" or "rad/s". */
    unit: string;
    series: [AxisChartSeries, AxisChartSeries, AxisChartSeries];
    data: AlignedData;
}) {
    const containerRef = useRef<HTMLDivElement>(null);
    const plotRef = useRef<uPlot | null>(null);
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
                ...series.map((s) => ({ label: s.label, stroke: s.color, width: 1 })),
            ],
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

        const observer = new ResizeObserver(() => {
            plot.setSize({ width: container.clientWidth || 600, height: HEIGHT });
        });
        observer.observe(container);

        return () => {
            observer.disconnect();
            plotRef.current = null;
            plot.destroy();
        };
        // `data` is intentionally NOT a dependency — it flows through setData below
        // without recreating the canvas.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [resolvedTheme, unit, series]);

    // Feed data changes into the existing instance.
    useEffect(() => {
        plotRef.current?.setData(data as uPlot.AlignedData);
    }, [data]);

    return (
        <section className="rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
            <h3 className="mb-2 text-sm font-medium text-slate-700 dark:text-slate-300">{title}</h3>
            <div ref={containerRef} />
        </section>
    );
}
