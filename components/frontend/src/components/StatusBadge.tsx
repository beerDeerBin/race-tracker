import { useTranslation } from 'react-i18next';
import { Activity, CircleSlash, Pause, Plug } from 'lucide-react';
import type { ComponentType } from 'react';
import type { DeviceState } from '../models/realtime';

const STYLES: Record<DeviceState, string> = {
    Idle: 'bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-200',
    Connected: 'bg-sky-100 text-sky-700 dark:bg-sky-900 dark:text-sky-300',
    Acquiring: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900 dark:text-emerald-300',
};

const KEYS: Record<DeviceState, string> = {
    Idle: 'status.idle',
    Connected: 'status.connected',
    Acquiring: 'status.acquiring',
};

const ICONS: Record<DeviceState, ComponentType<{ className?: string }>> = {
    Idle: Pause,
    Connected: Plug,
    Acquiring: Activity,
};

/** Live device-state pill (/F60/); `state === null` renders the "no signal yet" variant. */
export function StatusBadge({ state }: { state: DeviceState | null }) {
    const { t } = useTranslation();

    if (state === null) {
        return (
            <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-400 dark:bg-slate-800 dark:text-slate-500">
                <CircleSlash className="h-3 w-3" aria-hidden="true" />
                {t('status.unknown')}
            </span>
        );
    }

    const Icon = ICONS[state];
    return (
        <span
            className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium ${STYLES[state]} ${
                state === 'Acquiring' ? 'animate-pulse' : ''
            }`}
        >
            <Icon className="h-3 w-3" aria-hidden="true" />
            {t(KEYS[state])}
        </span>
    );
}
