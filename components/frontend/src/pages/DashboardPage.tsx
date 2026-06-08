import { useTranslation } from 'react-i18next';
import { useVehicles } from '../hooks/useVehicles';
import { VehicleList } from '../components/VehicleList';
import { PageShell } from '../components/PageShell';

/** The device dashboard (/F83/, /U20/): all vehicles incl. pending, with live status. */
export function DashboardPage() {
    const { t } = useTranslation();
    const { data: vehicles, isPending, isError } = useVehicles();

    return (
        <PageShell title={t('dashboard.title')}>
            {isPending && <p className="text-slate-500">{t('dashboard.loading')}</p>}
            {isError && (
                <p role="alert" className="text-red-600 dark:text-red-400">
                    {t('dashboard.loadFailed')}
                </p>
            )}
            {vehicles &&
                (vehicles.length === 0 ? (
                    <p className="text-slate-500 dark:text-slate-400">{t('dashboard.empty')}</p>
                ) : (
                    <VehicleList vehicles={vehicles} />
                ))}
        </PageShell>
    );
}
