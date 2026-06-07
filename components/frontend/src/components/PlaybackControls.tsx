import { useTranslation } from 'react-i18next';
import { PLAYBACK_SPEEDS } from '../hooks/usePlaybackClock';
import type { PlaybackClock } from '../hooks/usePlaybackClock';

/** Play/pause + time slider + speed selector for the trajectory playback (/U20/). */
export function PlaybackControls({ clock, duration }: { clock: PlaybackClock; duration: number }) {
    const { t } = useTranslation();

    return (
        <div className="mt-4 flex flex-wrap items-center gap-4 rounded-lg border border-slate-200 bg-white p-3 dark:border-slate-800 dark:bg-slate-900">
            <button
                type="button"
                onClick={clock.toggle}
                className="rounded bg-sky-600 px-4 py-1.5 text-sm font-medium text-white hover:bg-sky-500"
            >
                {clock.playing ? t('trajectory.pause') : t('trajectory.play')}
            </button>

            <input
                type="range"
                min={0}
                max={duration}
                step={duration / 1000 || 0.01}
                value={clock.time}
                onChange={(event) => clock.seek(Number(event.target.value))}
                aria-label={t('trajectory.timeSlider')}
                className="min-w-40 flex-1"
            />

            <span className="font-mono text-xs text-slate-500 tabular-nums dark:text-slate-400">
                {clock.time.toFixed(1)} / {duration.toFixed(1)} s
            </span>

            <div className="flex items-center gap-1">
                {PLAYBACK_SPEEDS.map((speed) => (
                    <button
                        key={speed}
                        type="button"
                        onClick={() => clock.setSpeed(speed)}
                        aria-pressed={clock.speed === speed}
                        className={`rounded px-2 py-1 text-xs font-medium ${
                            clock.speed === speed
                                ? 'bg-sky-600 text-white'
                                : 'border border-slate-300 text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800'
                        }`}
                    >
                        {speed}×
                    </button>
                ))}
            </div>
        </div>
    );
}
