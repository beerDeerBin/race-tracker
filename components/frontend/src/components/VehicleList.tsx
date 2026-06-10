import { useMemo, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Hand } from 'lucide-react';
import { StatusBadge } from './StatusBadge';
import { VehicleAvatar } from './VehicleAvatar';
import { ClaimDialog } from './ClaimDialog';
import { RunControls } from './RunControls';
import { RunProgressBar } from './RunProgressBar';
import { ErrorCodeList } from './ErrorCodeList';
import { TableToolbar } from './TableToolbar';
import { useDeviceStatus } from '../hooks/useDeviceStatus';
import { encodeGuid } from '../utils/encodeGuid';
import { formatBattery, formatUptime, secondsSince } from '../utils/format';
import type { VehicleResponse } from '../models/api';

type RegistrationFilter = 'all' | 'pending' | 'registered';

/**
 * The device dashboard table (/F83/): every vehicle (incl. pending) with its live status
 * (/F60/) — state, battery, uptime, error indicator, last-seen age. Pending rows offer
 * the claim action (/F25/, story 7.3). A search box + registration filter narrow the list
 * client-side (default: show all).
 */
export function VehicleList({ vehicles }: { vehicles: VehicleResponse[] }) {
    const { t } = useTranslation();
    const [claiming, setClaiming] = useState<VehicleResponse | null>(null);
    const [query, setQuery] = useState('');
    const [registration, setRegistration] = useState<RegistrationFilter>('all');

    const filtered = useMemo(() => {
        const q = query.trim().toLowerCase();
        return vehicles.filter((vehicle) => {
            if (registration !== 'all' && vehicle.registrationStatus !== registration) {
                return false;
            }
            if (!q) {
                return true;
            }
            return (
                vehicle.name.toLowerCase().includes(q) ||
                vehicle.owner.toLowerCase().includes(q) ||
                vehicle.deviceGuid.toLowerCase().includes(q)
            );
        });
    }, [vehicles, query, registration]);

    const th = 'px-4 py-3 md:text-center';

    return (
        <div>
            <TableToolbar
                value={query}
                onChange={setQuery}
                placeholder={t('filters.searchVehicles')}
            >
                <select
                    value={registration}
                    onChange={(event) => setRegistration(event.target.value as RegistrationFilter)}
                    aria-label={t('filters.registration')}
                    className="field"
                >
                    <option value="all">{t('filters.registrationAll')}</option>
                    <option value="pending">{t('filters.registrationPending')}</option>
                    <option value="registered">{t('filters.registrationRegistered')}</option>
                </select>
            </TableToolbar>

            <div className="md:overflow-x-auto md:rounded-lg md:border md:border-slate-200 md:bg-white md:shadow-sm md:dark:border-slate-800 md:dark:bg-slate-900">
                {/* On md+ a normal table; below md each row stacks into a card with the column
                    header shown inline as a per-cell label (the real <thead> is hidden). */}
                <table className="block w-full text-left text-sm md:table">
                    <thead className="hidden border-b border-slate-200 text-xs text-slate-500 uppercase md:table-header-group dark:border-slate-800 dark:text-slate-400">
                        <tr>
                            <th className={th}>
                                <span className="sr-only">{t('vehicles.image')}</span>
                            </th>
                            <th className={`${th} md:text-left`}>{t('vehicles.name')}</th>
                            <th className={th}>{t('vehicles.owner')}</th>
                            <th className={th}>{t('vehicles.registration')}</th>
                            <th className={th}>{t('vehicles.state')}</th>
                            <th className={th}>{t('vehicles.battery')}</th>
                            <th className={th}>{t('vehicles.uptime')}</th>
                            <th className={th}>{t('vehicles.lastSeen')}</th>
                            <th className={th}>{t('vehicles.actions')}</th>
                        </tr>
                    </thead>
                    <tbody className="block md:table-row-group">
                        {filtered.map((vehicle) => (
                            <VehicleRow
                                key={vehicle.deviceGuid}
                                vehicle={vehicle}
                                onClaim={() => setClaiming(vehicle)}
                            />
                        ))}
                    </tbody>
                </table>
                {filtered.length === 0 && (
                    <p className="px-4 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                        {t('filters.noMatches')}
                    </p>
                )}
            </div>
            {claiming && <ClaimDialog vehicle={claiming} onClose={() => setClaiming(null)} />}
        </div>
    );
}

/** The column header repeated inline on each cell when the table is stacked below `md` (the real
 *  <thead> is hidden there); collapses away on `md+` where the header row provides the labels. */
function MobileLabel({ children }: { children: ReactNode }) {
    return (
        <span className="mr-2 inline-block font-medium text-slate-500 uppercase md:hidden dark:text-slate-400">
            {children}
        </span>
    );
}

function VehicleRow({ vehicle, onClaim }: { vehicle: VehicleResponse; onClaim: () => void }) {
    const { t } = useTranslation();
    const status = useDeviceStatus(vehicle.deviceGuid);

    const td = 'block py-1 md:table-cell md:px-4 md:py-3 md:text-center';

    return (
        <tr className="mb-3 block rounded-lg border border-slate-200 bg-white p-3 transition-colors last:mb-0 md:mb-0 md:table-row md:rounded-none md:border-0 md:border-b md:border-slate-100 md:bg-transparent md:p-0 md:last:border-0 md:hover:bg-slate-50 dark:border-slate-800 dark:bg-slate-900 md:dark:bg-transparent md:dark:hover:bg-slate-800/40">
            <td className={`${td} md:w-20`}>
                <MobileLabel>{t('vehicles.image')}</MobileLabel>
                <div className="flex md:justify-center">
                    <VehicleAvatar vehicle={vehicle} className="h-11 w-11" />
                </div>
            </td>
            <td className={`${td} md:px-6 md:text-left`}>
                <MobileLabel>{t('vehicles.name')}</MobileLabel>
                <Link
                    to={`/vehicles/${encodeGuid(vehicle.deviceGuid)}`}
                    className="font-medium text-f1-red transition-colors hover:text-f1-red-hi hover:underline"
                >
                    {vehicle.name}
                </Link>
                <div className="font-mono text-xs break-all text-slate-400 md:whitespace-nowrap dark:text-slate-500">
                    {vehicle.deviceGuid}
                </div>
            </td>
            <td className={`${td} text-slate-600 dark:text-slate-300`}>
                <MobileLabel>{t('vehicles.owner')}</MobileLabel>
                {vehicle.owner || '—'}
            </td>
            <td className={td}>
                <MobileLabel>{t('vehicles.registration')}</MobileLabel>
                {vehicle.registrationStatus === 'pending' ? (
                    <span className="inline-block rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-medium text-amber-700 dark:bg-amber-900 dark:text-amber-300">
                        {t('vehicles.pending')}
                    </span>
                ) : (
                    <span className="inline-block rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                        {t('vehicles.registered')}
                    </span>
                )}
            </td>
            <td className={td}>
                <MobileLabel>{t('vehicles.state')}</MobileLabel>
                <StatusBadge state={status?.state ?? null} />
                {status && <ErrorCodeList errorCode={status.errorCode} />}
                {status?.state === 'Acquiring' && (
                    <RunProgressBar deviceGuid={vehicle.deviceGuid} />
                )}
            </td>
            <td className={`${td} text-slate-600 dark:text-slate-300`}>
                <MobileLabel>{t('vehicles.battery')}</MobileLabel>
                {status ? formatBattery(status.batteryMv, status.batteryPct) : '—'}
            </td>
            <td className={`${td} text-slate-600 dark:text-slate-300`}>
                <MobileLabel>{t('vehicles.uptime')}</MobileLabel>
                {status ? formatUptime(status.uptimeMs) : '—'}
            </td>
            <td className={`${td} text-slate-500 dark:text-slate-400`}>
                <MobileLabel>{t('vehicles.lastSeen')}</MobileLabel>
                {status
                    ? t('vehicles.secondsAgo', { seconds: secondsSince(status.observedAtUtc) })
                    : '—'}
            </td>
            <td className={td}>
                <MobileLabel>{t('vehicles.actions')}</MobileLabel>
                <div className="flex flex-wrap items-center gap-1.5 md:flex-nowrap md:justify-center">
                    {vehicle.registrationStatus === 'pending' && (
                        <button
                            type="button"
                            onClick={onClaim}
                            className="inline-flex items-center gap-1 rounded bg-f1-red px-3 py-1 text-xs font-medium whitespace-nowrap text-white transition-colors hover:bg-f1-red-hi"
                        >
                            <Hand className="h-3.5 w-3.5" aria-hidden="true" />
                            {t('claim.button')}
                        </button>
                    )}
                    <RunControls deviceGuid={vehicle.deviceGuid} state={status?.state ?? null} />
                </div>
            </td>
        </tr>
    );
}
