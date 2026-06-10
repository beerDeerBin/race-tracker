import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { X } from 'lucide-react';
import { AuthImage } from './AuthImage';

/**
 * Full-screen preview of a single gallery image, reused by the dashboard avatar and the gallery.
 * Mirrors the app's dialog pattern (role="dialog", aria-modal, Escape + click-outside close); focus
 * moves to the close button on open so keyboard users can dismiss immediately.
 */
export function ImagePreviewDialog({
    deviceGuid,
    imageId,
    alt,
    onClose,
}: {
    deviceGuid: string;
    imageId: string;
    alt: string;
    onClose: () => void;
}) {
    const { t } = useTranslation();
    const closeRef = useRef<HTMLButtonElement>(null);

    useEffect(() => closeRef.current?.focus(), []);

    return (
        <div
            className="fixed inset-0 z-20 flex items-center justify-center bg-black/80 p-4"
            role="presentation"
            onClick={onClose}
        >
            <div
                role="dialog"
                aria-modal="true"
                aria-label={t('imagePreview.title')}
                onClick={(event) => event.stopPropagation()}
                onKeyDown={(event) => {
                    if (event.key === 'Escape') {
                        onClose();
                    }
                }}
                className="relative flex max-h-full max-w-full flex-col"
            >
                <button
                    ref={closeRef}
                    type="button"
                    onClick={onClose}
                    aria-label={t('imagePreview.close')}
                    className="absolute -top-2 -right-2 z-10 rounded-full bg-white p-1.5 text-slate-700 shadow-lg transition-colors hover:text-f1-red focus:ring-2 focus:ring-f1-red focus:outline-none dark:bg-slate-800 dark:text-slate-200"
                >
                    <X className="h-5 w-5" aria-hidden="true" />
                </button>
                <AuthImage
                    deviceGuid={deviceGuid}
                    imageId={imageId}
                    alt={alt}
                    className="max-h-[85vh] max-w-[90vw] rounded-lg object-contain shadow-2xl"
                />
            </div>
        </div>
    );
}
