import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { PageShell } from '../components/PageShell';
import { AxisChart } from '../components/AxisChart';
import type { AxisChartBand, AxisChartSeries } from '../components/AxisChart';
import { ChartToolbar } from '../components/ChartToolbar';
import type { AxisVisibility, ChartView } from '../components/ChartToolbar';
import { useRuns } from '../hooks/useRuns';
import { useSamples } from '../hooks/useSamples';
import { useRunRollup } from '../hooks/useRunRollup';
import { useLiveRun } from '../hooks/useLiveRun';
import { effectiveOdrHz } from '../utils/odr';
import { encodeGuid } from '../utils/encodeGuid';
import {
    filterBucketsByTime,
    filterSamplesByTime,
    toAccelData,
    toAccelRollupData,
    toGyroData,
    toGyroRollupData,
} from '../utils/chartData';

// Module-level constants keep the AxisChart create-effect stable across renders.
const ACCEL_SERIES: AxisChartSeries[] = [
    { label: 'ax', color: '#ef4444' },
    { label: 'ay', color: '#22c55e' },
    { label: 'az', color: '#3b82f6' },
];
const GYRO_SERIES: AxisChartSeries[] = [
    { label: 'gx', color: '#f97316' },
    { label: 'gy', color: '#14b8a6' },
    { label: 'gz', color: '#a855f7' },
];

// Aggregate view: per axis min (dashed) / max (dashed) / avg (solid) + min↔max band.
function rollupSeries(labels: [string, string, string], colors: [string, string, string]) {
    return labels.flatMap((label, i) => [
        { label: `${label} min`, color: colors[i]!, width: 0.5, dash: [4, 4] },
        { label: `${label} max`, color: colors[i]!, width: 0.5, dash: [4, 4] },
        { label: `${label} avg`, color: colors[i]!, width: 1.5 },
    ]);
}
const ACCEL_ROLLUP_SERIES: AxisChartSeries[] = rollupSeries(
    ['ax', 'ay', 'az'],
    ['#ef4444', '#22c55e', '#3b82f6'],
);
const GYRO_ROLLUP_SERIES: AxisChartSeries[] = rollupSeries(
    ['gx', 'gy', 'gz'],
    ['#f97316', '#14b8a6', '#a855f7'],
);
// Column layout per axis: [min, max, avg] → band fills min↔max (1-based uPlot indices).
const ROLLUP_BANDS: AxisChartBand[] = [
    { from: 2, to: 1, fill: 'rgba(148, 163, 184, 0.15)' },
    { from: 5, to: 4, fill: 'rgba(148, 163, 184, 0.15)' },
    { from: 8, to: 7, fill: 'rgba(148, 163, 184, 0.15)' },
];

/**
 * Run detail (/F81/, /F82/, /F53/, /D50/): the six axes as two diagrams — accel (m/s²)
 * and gyro (rad/s) — over time from ODR, with time-range/axis filters and a raw ↔
 * aggregate (4.2 roll-up) toggle. Pure display; data comes from GraphQL.
 */
export function RunDetailPage() {
    const { t } = useTranslation();
    const { deviceGuid = '', runId = '' } = useParams<{ deviceGuid: string; runId: string }>();

    const [view, setView] = useState<ChartView>('raw');
    const [fromSeconds, setFromSeconds] = useState<number | null>(null);
    const [toSeconds, setToSeconds] = useState<number | null>(null);
    const [axes, setAxes] = useState<AxisVisibility>({ x: true, y: true, z: true });

    const { data: runs } = useRuns(deviceGuid);
    const run = runs?.find((r) => r.runId === runId) ?? null;
    const { data: samples, isPending, isError } = useSamples(runId);
    const rollup = useRunRollup(runId, view === 'aggregate');

    // Live append (/F64/): new batches grow the chart without a reload while the run runs.
    useLiveRun(deviceGuid, runId);

    const odrHz = effectiveOdrHz(run);

    const filteredSamples = useMemo(
        () => filterSamplesByTime(samples ?? [], odrHz, fromSeconds, toSeconds),
        [samples, odrHz, fromSeconds, toSeconds],
    );
    const filteredBuckets = useMemo(
        () => filterBucketsByTime(rollup.data ?? [], odrHz, fromSeconds, toSeconds),
        [rollup.data, odrHz, fromSeconds, toSeconds],
    );

    const accelData = useMemo(
        () =>
            view === 'raw'
                ? toAccelData(filteredSamples, odrHz)
                : toAccelRollupData(filteredBuckets, odrHz),
        [view, filteredSamples, filteredBuckets, odrHz],
    );
    const gyroData = useMemo(
        () =>
            view === 'raw'
                ? toGyroData(filteredSamples, odrHz)
                : toGyroRollupData(filteredBuckets, odrHz),
        [view, filteredSamples, filteredBuckets, odrHz],
    );

    // Visibility flags follow the active series layout: 3 per chart raw, 9 aggregate.
    const visibility = useMemo(() => {
        const flags = [axes.x, axes.y, axes.z];
        return view === 'raw' ? flags : flags.flatMap((show) => [show, show, show]);
    }, [view, axes]);

    // Symmetric empty handling: a range filter that excludes everything shows the empty
    // message in both views instead of blank axes.
    const isEmpty =
        view === 'raw'
            ? samples !== undefined && filteredSamples.length === 0
            : rollup.isSuccess && filteredBuckets.length === 0;
    const showCharts =
        view === 'raw'
            ? samples !== undefined && filteredSamples.length > 0
            : rollup.data !== undefined && filteredBuckets.length > 0;

    return (
        <PageShell title={t('runDetail.title')}>
            <div className="mb-2 flex items-center justify-between">
                <Link
                    to={`/vehicles/${encodeGuid(deviceGuid)}`}
                    className="inline-block text-sm text-sky-600 hover:underline dark:text-sky-400"
                >
                    ← {t('runDetail.backToRuns')}
                </Link>
                <Link
                    to={`/vehicles/${encodeGuid(deviceGuid)}/runs/${encodeURIComponent(runId)}/trajectory`}
                    className="rounded bg-sky-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-sky-500"
                >
                    {t('runDetail.viewTrajectory')}
                </Link>
            </div>
            <p className="mb-4 font-mono text-xs text-slate-400 dark:text-slate-500">
                {runId} · {t('runDetail.meta', { count: samples?.length ?? 0, odr: odrHz })}
            </p>

            <ChartToolbar
                view={view}
                onViewChange={setView}
                fromSeconds={fromSeconds}
                toSeconds={toSeconds}
                onRangeChange={(from, to) => {
                    setFromSeconds(from);
                    setToSeconds(to);
                }}
                axes={axes}
                onAxesChange={setAxes}
            />

            {(view === 'raw' ? isPending : rollup.isPending) && (
                <p className="text-slate-500">{t('runDetail.loading')}</p>
            )}
            {(view === 'raw' ? isError : rollup.isError) && (
                <p role="alert" className="text-red-600 dark:text-red-400">
                    {t('runDetail.loadFailed')}
                </p>
            )}
            {isEmpty && (
                <p className="text-slate-500 dark:text-slate-400">{t('runDetail.empty')}</p>
            )}

            {showCharts && (
                <div className="space-y-6">
                    <AxisChart
                        title={t('runDetail.accelTitle')}
                        unit="m/s²"
                        series={view === 'raw' ? ACCEL_SERIES : ACCEL_ROLLUP_SERIES}
                        data={accelData}
                        bands={view === 'aggregate' ? ROLLUP_BANDS : undefined}
                        visibility={visibility}
                    />
                    <AxisChart
                        title={t('runDetail.gyroTitle')}
                        unit="rad/s"
                        series={view === 'raw' ? GYRO_SERIES : GYRO_ROLLUP_SERIES}
                        data={gyroData}
                        bands={view === 'aggregate' ? ROLLUP_BANDS : undefined}
                        visibility={visibility}
                    />
                </div>
            )}
        </PageShell>
    );
}
