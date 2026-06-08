import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { StatusBadge } from './StatusBadge';
import { ClaimDialog } from './ClaimDialog';
import { RunControls } from './RunControls';
import { RunProgressBar } from './RunProgressBar';
import { ErrorCodeList } from './ErrorCodeList';
import { useDeviceStatus } from '../hooks/useDeviceStatus';
import { encodeGuid } from '../utils/encodeGuid';
import { formatBattery, formatUptime, secondsSince } from '../utils/format';
import type { VehicleResponse } from '../models/api';

/**
 * The device dashboard table (/F83/): every vehicle (incl. pending) with its live status
 * (/F60/) — state, battery, uptime, error indicator, last-seen age. Pending rows offer
 * the claim action (/F25/, story 7.3).
 */
export function VehicleList({ vehicles }: { vehicles: VehicleResponse[] }) {
    const { t } = useTranslation();
    const [claiming, setClaiming] = useState<VehicleResponse | null>(null);

    return (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
            <table className="w-full text-left text-sm">
                <thead className="border-b border-slate-200 text-xs text-slate-500 uppercase dark:border-slate-800 dark:text-slate-400">
                    <tr>
                        <th className="px-4 py-3">{t('vehicles.name')}</th>
                        <th className="px-4 py-3">{t('vehicles.owner')}</th>
                        <th className="px-4 py-3">{t('vehicles.registration')}</th>
                        <th className="px-4 py-3">{t('vehicles.state')}</th>
                        <th className="px-4 py-3">{t('vehicles.battery')}</th>
                        <th className="px-4 py-3">{t('vehicles.uptime')}</th>
                        <th className="px-4 py-3">{t('vehicles.lastSeen')}</th>
                        <th className="px-4 py-3">{t('vehicles.actions')}</th>
                    </tr>
                </thead>
                <tbody>
                    {vehicles.map((vehicle) => (
                        <VehicleRow
                            key={vehicle.deviceGuid}
                            vehicle={vehicle}
                            onClaim={() => setClaiming(vehicle)}
                        />
                    ))}
                </tbody>
            </table>
            {claiming && <ClaimDialog vehicle={claiming} onClose={() => setClaiming(null)} />}
        </div>
    );
}

function VehicleRow({ vehicle, onClaim }: { vehicle: VehicleResponse; onClaim: () => void }) {
    const { t } = useTranslation();
    const status = useDeviceStatus(vehicle.deviceGuid);

    return (
        <tr className="border-b border-slate-100 last:border-0 dark:border-slate-800">
            <td className="px-4 py-3">
                <Link
                    to={`/vehicles/${encodeGuid(vehicle.deviceGuid)}`}
                    className="font-medium text-sky-700 hover:underline dark:text-sky-400"
                >
                    {vehicle.name}
                </Link>
                <div className="font-mono text-xs text-slate-400 dark:text-slate-500">
                    {vehicle.deviceGuid}
                </div>
            </td>
            <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{vehicle.owner}</td>
            <td className="px-4 py-3">
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
            <td className="px-4 py-3">
                <StatusBadge state={status?.state ?? null} />
                {status && <ErrorCodeList errorCode={status.errorCode} />}
                {status?.state === 'Acquiring' && (
                    <RunProgressBar deviceGuid={vehicle.deviceGuid} />
                )}
            </td>
            <td className="px-4 py-3 text-slate-600 dark:text-slate-300">
                {status ? formatBattery(status.batteryMv, status.batteryPct) : '—'}
            </td>
            <td className="px-4 py-3 text-slate-600 dark:text-slate-300">
                {status ? formatUptime(status.uptimeMs) : '—'}
            </td>
            <td className="px-4 py-3 text-slate-500 dark:text-slate-400">
                {status
                    ? t('vehicles.secondsAgo', { seconds: secondsSince(status.observedAtUtc) })
                    : '—'}
            </td>
            <td className="px-4 py-3">
                <div className="flex flex-wrap items-center gap-1.5">
                    {vehicle.registrationStatus === 'pending' && (
                        <button
                            type="button"
                            onClick={onClaim}
                            className="rounded bg-sky-600 px-3 py-1 text-xs font-medium text-white hover:bg-sky-500"
                        >
                            {t('claim.button')}
                        </button>
                    )}
                    <RunControls deviceGuid={vehicle.deviceGuid} state={status?.state ?? null} />
                </div>
            </td>
        </tr>
    );
}
