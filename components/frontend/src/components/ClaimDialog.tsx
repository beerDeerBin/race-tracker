import { useState } from 'react';
import type { FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useClaim } from '../hooks/useClaim';
import { useAuth } from '../hooks/useAuth';
import { DeviceNotFoundError } from '../services/vehicleService';
import type { VehicleResponse } from '../models/api';

/**
 * Claim dialog (/F25/): names a pending device and takes it over. Owner is optional —
 * the backend defaults it to the authenticated user.
 */
export function ClaimDialog({
    vehicle,
    onClose,
}: {
    vehicle: VehicleResponse;
    onClose: () => void;
}) {
    const { t } = useTranslation();
    const { user } = useAuth();
    const claim = useClaim();

    const [name, setName] = useState('');
    const [owner, setOwner] = useState('');

    const handleSubmit = (event: FormEvent) => {
        event.preventDefault();
        const trimmedName = name.trim();
        if (!trimmedName) {
            // HTML `required` lets whitespace-only values through — never claim a
            // device with an empty name.
            return;
        }
        const trimmedOwner = owner.trim();
        claim.mutate(
            {
                deviceGuid: vehicle.deviceGuid,
                request: {
                    name: trimmedName,
                    ...(trimmedOwner ? { owner: trimmedOwner } : {}),
                },
            },
            { onSuccess: onClose },
        );
    };

    const errorKey =
        claim.isError &&
        (claim.error instanceof DeviceNotFoundError ? 'claim.notFound' : 'claim.failed');

    const inputClasses =
        'w-full rounded border border-slate-300 bg-white px-3 py-2 text-slate-900 focus:border-sky-500 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100';

    return (
        <div
            className="fixed inset-0 z-10 flex items-center justify-center bg-black/40 p-4"
            role="presentation"
            onClick={onClose}
        >
            <form
                role="dialog"
                aria-modal="true"
                aria-labelledby="claim-dialog-title"
                onClick={(event) => event.stopPropagation()}
                onKeyDown={(event) => {
                    if (event.key === 'Escape') {
                        onClose();
                    }
                }}
                onSubmit={handleSubmit}
                className="w-full max-w-sm space-y-4 rounded-lg bg-white p-6 shadow-xl dark:bg-slate-900"
            >
                <h2
                    id="claim-dialog-title"
                    className="text-lg font-semibold text-slate-900 dark:text-slate-100"
                >
                    {t('claim.title')}
                </h2>
                <p className="font-mono text-xs break-all text-slate-400 dark:text-slate-500">
                    {vehicle.deviceGuid}
                </p>

                <label className="block">
                    <span className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">
                        {t('claim.name')}
                    </span>
                    <input
                        type="text"
                        value={name}
                        onChange={(event) => setName(event.target.value)}
                        required
                        autoFocus
                        className={inputClasses}
                    />
                </label>

                <label className="block">
                    <span className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">
                        {t('claim.owner')}
                    </span>
                    <input
                        type="text"
                        value={owner}
                        onChange={(event) => setOwner(event.target.value)}
                        placeholder={t('claim.ownerPlaceholder', { user })}
                        className={inputClasses}
                    />
                </label>

                {errorKey && (
                    <p role="alert" className="text-sm text-red-600 dark:text-red-400">
                        {t(errorKey)}
                    </p>
                )}

                <div className="flex justify-end gap-2">
                    <button
                        type="button"
                        onClick={onClose}
                        className="rounded border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
                    >
                        {t('claim.cancel')}
                    </button>
                    <button
                        type="submit"
                        disabled={claim.isPending}
                        className="rounded bg-sky-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-sky-500 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                        {claim.isPending ? t('claim.claiming') : t('claim.submit')}
                    </button>
                </div>
            </form>
        </div>
    );
}
