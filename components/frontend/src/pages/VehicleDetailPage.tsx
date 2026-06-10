import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ChevronLeft } from 'lucide-react';
import { PageShell } from '../components/PageShell';
import { RunList } from '../components/RunList';
import { Tabs } from '../components/Tabs';
import { VehicleGallery } from '../components/VehicleGallery';
import { VehicleSettings } from '../components/VehicleSettings';
import { useRuns } from '../hooks/useRuns';
import { useVehicles } from '../hooks/useVehicles';

/** A vehicle's detail page (/F80/): the run list and the image gallery as two tabs. */
export function VehicleDetailPage() {
    const { t } = useTranslation();
    // useParams decodes the URI component — the guid arrives verbatim (case preserved).
    const { deviceGuid = '' } = useParams<{ deviceGuid: string }>();
    const { data: runs, isPending, isError } = useRuns(deviceGuid);
    const { data: vehicles } = useVehicles();

    const vehicle = vehicles?.find((v) => v.deviceGuid === deviceGuid);
    const vehicleName = vehicle?.name ?? deviceGuid;

    const runsPanel = (
        <>
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
        </>
    );

    return (
        <PageShell title={t('runs.title', { vehicle: vehicleName })}>
            <Link
                to="/"
                className="mb-4 inline-flex items-center gap-1 text-sm font-medium text-f1-red transition-colors hover:text-f1-red-hi hover:underline"
            >
                <ChevronLeft className="h-4 w-4" aria-hidden="true" />
                {t('runs.backToDashboard')}
            </Link>
            <Tabs
                tabs={[
                    { id: 'runs', label: t('vehicleDetail.runsTab'), panel: runsPanel },
                    {
                        id: 'gallery',
                        label: t('vehicleDetail.galleryTab'),
                        // The gallery needs the vehicle (guid + current title image); render it only
                        // once the vehicle list has loaded so titleImageId is known.
                        panel: vehicle ? (
                            <VehicleGallery vehicle={vehicle} />
                        ) : (
                            <p className="text-slate-500">{t('gallery.loading')}</p>
                        ),
                    },
                    {
                        id: 'settings',
                        label: t('vehicleDetail.settingsTab'),
                        panel: vehicle ? (
                            <VehicleSettings vehicle={vehicle} />
                        ) : (
                            <p className="text-slate-500">{t('gallery.loading')}</p>
                        ),
                    },
                ]}
            />
        </PageShell>
    );
}
