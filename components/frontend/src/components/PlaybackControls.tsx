import { useTranslation } from 'react-i18next';
import { Pause, Play, RotateCcw } from 'lucide-react';
import { PLAYBACK_SPEEDS } from '../hooks/usePlaybackClock';
import type { PlaybackClock } from '../hooks/usePlaybackClock';

/** Play/pause + time slider + speed selector for the trajectory playback (/U20/). */
export function PlaybackControls({ clock, duration }: { clock: PlaybackClock; duration: number }) {
    const { t } = useTranslation();

    // At the end the run is paused at full; toggling restarts it (usePlaybackClock.play resets), so
    // the button reads "Restart" with a rewind icon there instead of an inert "Play".
    const atEnd = duration > 0 && clock.time >= duration;
    const PlayPauseIcon = clock.playing ? Pause : atEnd ? RotateCcw : Play;
    const playPauseLabel = clock.playing
        ? t('trajectory.pause')
        : atEnd
          ? t('trajectory.restart')
          : t('trajectory.play');

    return (
        <div className="card mt-4 flex flex-wrap items-center gap-4 p-3">
            <button type="button" onClick={clock.toggle} className="btn-primary">
                <PlayPauseIcon className="h-4 w-4" aria-hidden="true" />
                {playPauseLabel}
            </button>

            <input
                type="range"
                min={0}
                max={duration}
                step={duration / 1000 || 0.01}
                value={clock.time}
                onChange={(event) => clock.seek(Number(event.target.value))}
                aria-label={t('trajectory.timeSlider')}
                className="min-w-40 flex-1 accent-f1-red"
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
                        className={`rounded px-2 py-1 text-xs font-medium transition-colors ${
                            clock.speed === speed
                                ? 'bg-f1-red text-white'
                                : 'border border-slate-300 text-slate-700 hover:border-f1-red hover:text-f1-red dark:border-slate-700 dark:text-slate-300'
                        }`}
                    >
                        {speed}×
                    </button>
                ))}
            </div>
        </div>
    );
}
