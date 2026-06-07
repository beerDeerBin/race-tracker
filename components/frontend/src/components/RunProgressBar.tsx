import { useTranslation } from 'react-i18next';
import { useRunProgress } from '../hooks/useRunProgress';

/**
 * Live run progress (/F61/): a small bar with "sampled / total" below the state badge
 * while the device is acquiring. Renders nothing before the first progress event.
 */
export function RunProgressBar({ deviceGuid }: { deviceGuid: string }) {
    const { t } = useTranslation();
    const progress = useRunProgress(deviceGuid);

    if (!progress || progress.totalSamples === 0) {
        return null;
    }

    const percent = Math.min(
        100,
        Math.round((progress.sampledCount / progress.totalSamples) * 100),
    );

    return (
        <div className="mt-1.5 w-36">
            <div className="h-1.5 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-700">
                <div
                    role="progressbar"
                    aria-valuenow={percent}
                    aria-valuemin={0}
                    aria-valuemax={100}
                    className="h-full rounded-full bg-emerald-500 transition-[width] duration-300"
                    style={{ width: `${percent}%` }}
                />
            </div>
            <div className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">
                {t('commands.progress', {
                    sampled: progress.sampledCount,
                    total: progress.totalSamples,
                    percent,
                })}
            </div>
        </div>
    );
}
