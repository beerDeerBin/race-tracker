import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Save, UserMinus } from 'lucide-react';
import { ConfirmDialog } from './ConfirmDialog';
import { useUnclaimVehicle, useUpdateVehicle } from '../hooks/useVehicleMutations';
import type { VehicleResponse } from '../models/api';

/**
 * The "Car settings" tab: rename the vehicle and release (un-claim) it. Releasing returns the
 * vehicle to the pending pool (owner cleared, name reset to its guid) and navigates back to the
 * dashboard, since the detail view is no longer "owned". Both actions go through the vehicle
 * mutation hooks; the destructive release is gated behind a confirmation.
 */
export function VehicleSettings({ vehicle }: { vehicle: VehicleResponse }) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const update = useUpdateVehicle(vehicle.deviceGuid);
    const unclaim = useUnclaimVehicle(vehicle.deviceGuid);

    const [name, setName] = useState(vehicle.name);
    const [confirming, setConfirming] = useState(false);

    const onRename = (event: FormEvent) => {
        event.preventDefault();
        const trimmed = name.trim();
        if (!trimmed) {
            return;
        }
        update.mutate({ name: trimmed });
    };

    const confirmUnclaim = () => unclaim.mutate(undefined, { onSuccess: () => navigate('/') });

    const inputClasses =
        'w-full rounded border border-slate-300 bg-white px-3 py-2 text-slate-900 transition-colors outline-none focus:border-f1-red focus:ring-1 focus:ring-f1-red dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100';

    return (
        <div className="max-w-md space-y-8">
            <form onSubmit={onRename} className="space-y-3">
                <h3 className="text-base font-semibold text-slate-900 dark:text-slate-100">
                    {t('settings.renameTitle')}
                </h3>
                <label className="block">
                    <span className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">
                        {t('settings.name')}
                    </span>
                    <input
                        type="text"
                        value={name}
                        onChange={(event) => setName(event.target.value)}
                        required
                        className={inputClasses}
                    />
                </label>
                {update.isError && (
                    <p role="alert" className="text-sm text-red-600 dark:text-red-400">
                        {t('settings.saveFailed')}
                    </p>
                )}
                {update.isSuccess && !update.isPending && (
                    <p className="text-sm text-emerald-600 dark:text-emerald-400">
                        {t('settings.saved')}
                    </p>
                )}
                <button
                    type="submit"
                    disabled={update.isPending || name.trim() === ''}
                    className="btn-primary"
                >
                    <Save className="h-4 w-4" aria-hidden="true" />
                    {update.isPending ? t('settings.saving') : t('settings.save')}
                </button>
            </form>

            <div className="space-y-3 border-t border-slate-200 pt-6 dark:border-slate-800">
                <h3 className="text-base font-semibold text-red-600 dark:text-red-400">
                    {t('settings.dangerZone')}
                </h3>
                <p className="text-sm text-slate-600 dark:text-slate-300">
                    {t('settings.unclaimHint')}
                </p>
                <button
                    type="button"
                    onClick={() => setConfirming(true)}
                    className="inline-flex items-center justify-center gap-1.5 rounded border border-red-600 px-3 py-1.5 text-sm font-medium text-red-600 transition-colors hover:bg-red-600 hover:text-white focus:ring-2 focus:ring-red-500 focus:outline-none dark:border-red-400 dark:text-red-400"
                >
                    <UserMinus className="h-4 w-4" aria-hidden="true" />
                    {t('settings.unclaim')}
                </button>
                {unclaim.isError && (
                    <p role="alert" className="text-sm text-red-600 dark:text-red-400">
                        {t('settings.unclaimFailed')}
                    </p>
                )}
            </div>

            {confirming && (
                <ConfirmDialog
                    title={t('settings.unclaimTitle')}
                    message={t('settings.unclaimConfirm', { name: vehicle.name })}
                    confirmLabel={t('settings.unclaim')}
                    danger
                    busy={unclaim.isPending}
                    onConfirm={confirmUnclaim}
                    onClose={() => setConfirming(false)}
                />
            )}
        </div>
    );
}
