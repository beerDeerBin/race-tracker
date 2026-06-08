import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { PageShell } from '../components/PageShell';
import { RunList } from '../components/RunList';
import { useRuns } from '../hooks/useRuns';
import { useVehicles } from '../hooks/useVehicles';

/** A vehicle's run list (/F80/): reached from the dashboard, links into run details. */
export function VehicleDetailPage() {
    const { t } = useTranslation();
    // useParams decodes the URI component — the guid arrives verbatim (case preserved).
    const { deviceGuid = '' } = useParams<{ deviceGuid: string }>();
    const { data: runs, isPending, isError } = useRuns(deviceGuid);
    const { data: vehicles } = useVehicles();

    const vehicleName = vehicles?.find((v) => v.deviceGuid === deviceGuid)?.name ?? deviceGuid;

    return (
        <PageShell title={t('runs.title', { vehicle: vehicleName })}>
            <Link
                to="/"
                className="mb-4 inline-block text-sm text-sky-600 hover:underline dark:text-sky-400"
            >
                ← {t('runs.backToDashboard')}
            </Link>
            {isPending && <p className="text-slate-500">{t('runs.loading')}</p>}
            {isError && (
                <p role="alert" className="text-red-600 dark:text-red-400">
                    {t('runs.loadFailed')}
                </p>
            )}
            {runs &&
                (runs.length === 0 ? (
                    <p className="text-slate-500 dark:text-slate-400">{t('runs.empty')}</p>
                ) : (
                    <RunList deviceGuid={deviceGuid} runs={runs} />
                ))}
        </PageShell>
    );
}
