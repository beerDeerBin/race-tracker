import { useMemo } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { PageShell } from '../components/PageShell';
import { AxisChart } from '../components/AxisChart';
import type { AxisChartSeries } from '../components/AxisChart';
import { useRuns } from '../hooks/useRuns';
import { useSamples } from '../hooks/useSamples';
import { effectiveOdrHz } from '../utils/odr';
import { encodeGuid } from '../utils/encodeGuid';
import { toAccelData, toGyroData } from '../utils/chartData';

// Module-level constants keep the AxisChart create-effect stable across renders.
const ACCEL_SERIES: [AxisChartSeries, AxisChartSeries, AxisChartSeries] = [
    { label: 'ax', color: '#ef4444' },
    { label: 'ay', color: '#22c55e' },
    { label: 'az', color: '#3b82f6' },
];
const GYRO_SERIES: [AxisChartSeries, AxisChartSeries, AxisChartSeries] = [
    { label: 'gx', color: '#f97316' },
    { label: 'gy', color: '#14b8a6' },
    { label: 'gz', color: '#a855f7' },
];

/**
 * Run detail (/F81/, /D50/): the six axes as two diagrams — accel (m/s²) and gyro
 * (rad/s) — over time from ODR. Pure display; data comes from GraphQL.
 */
export function RunDetailPage() {
    const { t } = useTranslation();
    const { deviceGuid = '', runId = '' } = useParams<{ deviceGuid: string; runId: string }>();

    const { data: runs } = useRuns(deviceGuid);
    const run = runs?.find((r) => r.runId === runId) ?? null;
    const { data: samples, isPending, isError } = useSamples(runId);

    const odrHz = effectiveOdrHz(run);
    const accelData = useMemo(() => toAccelData(samples ?? [], odrHz), [samples, odrHz]);
    const gyroData = useMemo(() => toGyroData(samples ?? [], odrHz), [samples, odrHz]);

    return (
        <PageShell title={t('runDetail.title')}>
            <Link
                to={`/vehicles/${encodeGuid(deviceGuid)}`}
                className="mb-2 inline-block text-sm text-sky-600 hover:underline dark:text-sky-400"
            >
                ← {t('runDetail.backToRuns')}
            </Link>
            <p className="mb-4 font-mono text-xs text-slate-400 dark:text-slate-500">
                {runId} · {t('runDetail.meta', { count: samples?.length ?? 0, odr: odrHz })}
            </p>

            {isPending && <p className="text-slate-500">{t('runDetail.loading')}</p>}
            {isError && (
                <p role="alert" className="text-red-600 dark:text-red-400">
                    {t('runDetail.loadFailed')}
                </p>
            )}
            {samples &&
                (samples.length === 0 ? (
                    <p className="text-slate-500 dark:text-slate-400">{t('runDetail.empty')}</p>
                ) : (
                    <div className="space-y-6">
                        <AxisChart
                            title={t('runDetail.accelTitle')}
                            unit="m/s²"
                            series={ACCEL_SERIES}
                            data={accelData}
                        />
                        <AxisChart
                            title={t('runDetail.gyroTitle')}
                            unit="rad/s"
                            series={GYRO_SERIES}
                            data={gyroData}
                        />
                    </div>
                ))}
        </PageShell>
    );
}
