import { useEffect, useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Hand, ImagePlus } from 'lucide-react';
import { useClaim } from '../hooks/useClaim';
import { useUploadImage } from '../hooks/useImageMutations';
import { useAuth } from '../hooks/useAuth';
import { DeviceNotFoundError } from '../services/vehicleService';
import { ALLOWED_IMAGE_TYPES, MAX_IMAGE_BYTES } from '../services/imageService';
import type { VehicleResponse } from '../models/api';

/**
 * Claim dialog (/F25/): names a pending device and takes it over. Owner is optional — the backend
 * defaults it to the authenticated user. An image can optionally be attached; on a successful claim
 * it is uploaded (becoming the title image automatically). An upload failure is non-blocking — the
 * claim still counts — and is surfaced as a warning so the user can retry from the gallery.
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
    const upload = useUploadImage(vehicle.deviceGuid);

    const [name, setName] = useState('');
    const [owner, setOwner] = useState('');
    const [file, setFile] = useState<File | null>(null);
    const [imageError, setImageError] = useState<string | null>(null);

    // Build (and clean up) a local object URL so the chosen file can be previewed before upload.
    // Created in the effect (not useMemo) so each mount holds a live URL under StrictMode.
    const [preview, setPreview] = useState<string | null>(null);
    /* eslint-disable react-hooks/set-state-in-effect */
    useEffect(() => {
        if (!file) {
            setPreview(null);
            return;
        }
        const url = URL.createObjectURL(file);
        setPreview(url);
        return () => URL.revokeObjectURL(url);
    }, [file]);
    /* eslint-enable react-hooks/set-state-in-effect */

    const onFileChange = (event: ChangeEvent<HTMLInputElement>) => {
        const chosen = event.target.files?.[0];
        event.target.value = '';
        if (!chosen) {
            return;
        }
        if (!ALLOWED_IMAGE_TYPES.includes(chosen.type as (typeof ALLOWED_IMAGE_TYPES)[number])) {
            setImageError('gallery.badType');
            setFile(null);
            return;
        }
        if (chosen.size > MAX_IMAGE_BYTES) {
            setImageError('gallery.tooLarge');
            setFile(null);
            return;
        }
        setImageError(null);
        setFile(chosen);
    };

    const handleSubmit = (event: FormEvent) => {
        event.preventDefault();
        const trimmedName = name.trim();
        if (!trimmedName) {
            // HTML `required` lets whitespace-only values through — never claim with an empty name.
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
            {
                onSuccess: () => {
                    if (file) {
                        // Upload the title image after the claim; close on success, warn on failure.
                        upload.mutate([file], { onSuccess: onClose });
                    } else {
                        onClose();
                    }
                },
            },
        );
    };

    const claimErrorKey =
        claim.isError &&
        (claim.error instanceof DeviceNotFoundError ? 'claim.notFound' : 'claim.failed');
    const errorKey =
        imageError ?? (claimErrorKey || (upload.isError ? 'claim.uploadFailed' : null));
    const busy = claim.isPending || upload.isPending;

    const inputClasses =
        'w-full rounded border border-slate-300 bg-white px-3 py-2 text-slate-900 transition-colors outline-none focus:border-f1-red focus:ring-1 focus:ring-f1-red dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100';

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

                <div className="block">
                    <span className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">
                        {t('claim.image')}
                    </span>
                    <div className="flex items-center gap-3">
                        {preview && (
                            <img
                                src={preview}
                                alt={t('claim.imagePreviewAlt')}
                                className="h-12 w-12 shrink-0 rounded-full border border-slate-200 object-cover dark:border-slate-700"
                            />
                        )}
                        <label className="btn-secondary cursor-pointer focus-within:ring-2 focus-within:ring-f1-red">
                            <ImagePlus className="h-4 w-4" aria-hidden="true" />
                            {file ? t('claim.imageChange') : t('claim.imageAdd')}
                            <input
                                type="file"
                                accept={ALLOWED_IMAGE_TYPES.join(',')}
                                onChange={onFileChange}
                                className="sr-only"
                            />
                        </label>
                    </div>
                </div>

                {errorKey && (
                    <p role="alert" className="text-sm text-red-600 dark:text-red-400">
                        {t(errorKey)}
                    </p>
                )}

                <div className="flex justify-end gap-2">
                    <button type="button" onClick={onClose} className="btn-secondary">
                        {t('claim.cancel')}
                    </button>
                    <button type="submit" disabled={busy} className="btn-primary">
                        <Hand className="h-4 w-4" aria-hidden="true" />
                        {busy ? t('claim.claiming') : t('claim.submit')}
                    </button>
                </div>
            </form>
        </div>
    );
}
