import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Car } from 'lucide-react';
import { AuthImage } from './AuthImage';
import { ImagePreviewDialog } from './ImagePreviewDialog';
import type { VehicleResponse } from '../models/api';

/**
 * Small circular vehicle avatar for the dashboard: the title image when set (click → full-screen
 * preview), otherwise a static "car" placeholder. When there is an image the avatar is a real button
 * (keyboard-focusable, Enter/Space) so the preview is reachable without a mouse.
 */
export function VehicleAvatar({
    vehicle,
    className = 'h-9 w-9',
}: {
    vehicle: VehicleResponse;
    className?: string;
}) {
    const { t } = useTranslation();
    const [previewing, setPreviewing] = useState(false);
    const titleImageId = vehicle.titleImageId;
    const ring =
        'shrink-0 overflow-hidden rounded-full border border-slate-200 bg-slate-100 dark:border-slate-700 dark:bg-slate-800';

    if (!titleImageId) {
        return (
            <div
                className={`flex items-center justify-center text-slate-400 dark:text-slate-500 ${ring} ${className}`}
                aria-hidden="true"
            >
                <Car className="h-1/2 w-1/2" />
            </div>
        );
    }

    const alt = t('vehicles.avatarAlt', { name: vehicle.name });

    return (
        <>
            <button
                type="button"
                onClick={() => setPreviewing(true)}
                aria-label={t('vehicles.openImage', { name: vehicle.name })}
                className={`${ring} ${className} transition-shadow hover:shadow-md focus:ring-2 focus:ring-f1-red focus:outline-none`}
            >
                <AuthImage
                    deviceGuid={vehicle.deviceGuid}
                    imageId={titleImageId}
                    alt={alt}
                    className="h-full w-full object-cover"
                />
            </button>
            {previewing && (
                <ImagePreviewDialog
                    deviceGuid={vehicle.deviceGuid}
                    imageId={titleImageId}
                    alt={alt}
                    onClose={() => setPreviewing(false)}
                />
            )}
        </>
    );
}
