import { useTranslation } from 'react-i18next';
import { ImageOff } from 'lucide-react';
import { useImageObjectUrl } from '../hooks/useImageObjectUrl';

/**
 * Renders a vehicle gallery image fetched through the authenticated HTTP instance (a plain
 * `<img src>` can't send the bearer token). Shows a pulse placeholder while loading and a fallback
 * icon on error. `className` is applied to whichever element renders, so the parent controls size.
 */
export function AuthImage({
    deviceGuid,
    imageId,
    alt,
    className = '',
}: {
    deviceGuid: string;
    imageId: string;
    alt: string;
    className?: string;
}) {
    const { t } = useTranslation();
    const { url, isPending, isError } = useImageObjectUrl(deviceGuid, imageId);

    if (isError) {
        return (
            <div
                role="img"
                aria-label={t('imagePreview.loadFailed')}
                className={`flex items-center justify-center bg-slate-100 text-slate-400 dark:bg-slate-800 dark:text-slate-500 ${className}`}
            >
                <ImageOff className="h-1/3 w-1/3" aria-hidden="true" />
            </div>
        );
    }

    if (!url || isPending) {
        return (
            <div
                aria-hidden="true"
                className={`animate-pulse bg-slate-200 dark:bg-slate-700 ${className}`}
            />
        );
    }

    return <img src={url} alt={alt} className={className} />;
}
