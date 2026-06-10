import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';

/**
 * Reusable confirmation dialog (delete an image, release a vehicle, …). Mirrors the app's dialog
 * pattern: role="dialog", aria-modal, Escape + click-outside cancel, and focus moves to the confirm
 * button on open. `danger` styles the confirm button red for destructive actions.
 */
export function ConfirmDialog({
    title,
    message,
    confirmLabel,
    onConfirm,
    onClose,
    danger = false,
    busy = false,
}: {
    title: string;
    message: string;
    confirmLabel: string;
    onConfirm: () => void;
    onClose: () => void;
    danger?: boolean;
    busy?: boolean;
}) {
    const { t } = useTranslation();
    const confirmRef = useRef<HTMLButtonElement>(null);

    useEffect(() => confirmRef.current?.focus(), []);

    return (
        <div
            className="fixed inset-0 z-30 flex items-center justify-center bg-black/50 p-4"
            role="presentation"
            onClick={onClose}
        >
            <div
                role="dialog"
                aria-modal="true"
                aria-labelledby="confirm-dialog-title"
                onClick={(event) => event.stopPropagation()}
                onKeyDown={(event) => {
                    if (event.key === 'Escape') {
                        onClose();
                    }
                }}
                className="w-full max-w-sm space-y-4 rounded-lg bg-white p-6 shadow-xl dark:bg-slate-900"
            >
                <h2
                    id="confirm-dialog-title"
                    className="text-lg font-semibold text-slate-900 dark:text-slate-100"
                >
                    {title}
                </h2>
                <p className="text-sm text-slate-600 dark:text-slate-300">{message}</p>
                <div className="flex justify-end gap-2">
                    <button type="button" onClick={onClose} className="btn-secondary">
                        {t('confirm.cancel')}
                    </button>
                    <button
                        ref={confirmRef}
                        type="button"
                        onClick={onConfirm}
                        disabled={busy}
                        className={
                            danger
                                ? 'inline-flex items-center justify-center gap-1.5 rounded bg-red-600 px-3 py-1.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-red-500 focus:ring-2 focus:ring-red-500 focus:outline-none disabled:cursor-not-allowed disabled:opacity-50'
                                : 'btn-primary'
                        }
                    >
                        {confirmLabel}
                    </button>
                </div>
            </div>
        </div>
    );
}
