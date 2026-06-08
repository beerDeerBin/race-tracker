import { useTranslation } from 'react-i18next';
import { useAuth } from '../hooks/useAuth';
import { useVehicles } from '../hooks/useVehicles';
import { VehicleList } from '../components/VehicleList';
import { ThemeToggle } from '../components/ThemeToggle';
import { LanguageSwitcher } from '../components/LanguageSwitcher';

/** The device dashboard (/F83/, /U20/): all vehicles incl. pending, with live status. */
export function DashboardPage() {
    const { t } = useTranslation();
    const { user, logout } = useAuth();
    const { data: vehicles, isPending, isError } = useVehicles();

    return (
        <div className="min-h-screen bg-slate-100 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
            <header className="flex items-center justify-between border-b border-slate-200 bg-white px-6 py-4 dark:border-slate-800 dark:bg-slate-900">
                <h1 className="text-xl font-semibold">{t('app.title')}</h1>
                <div className="flex items-center gap-4 text-sm">
                    <span className="text-slate-500 dark:text-slate-400">
                        {t('dashboard.loggedInAs', { user })}
                    </span>
                    <LanguageSwitcher />
                    <ThemeToggle />
                    <button
                        type="button"
                        onClick={logout}
                        className="rounded border border-slate-300 px-3 py-1.5 hover:bg-slate-200 dark:border-slate-700 dark:hover:bg-slate-800"
                    >
                        {t('dashboard.logout')}
                    </button>
                </div>
            </header>
            <main className="p-6">
                <h2 className="mb-4 text-lg font-medium">{t('dashboard.title')}</h2>
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
            </main>
        </div>
    );
}
