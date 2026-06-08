import { useMemo } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ChevronLeft } from 'lucide-react';
import { PageShell } from '../components/PageShell';
import { TrajectoryMap } from '../components/TrajectoryMap';
import { PlaybackControls } from '../components/PlaybackControls';
import { useTrajectory } from '../hooks/useTrajectory';
import { usePlaybackClock } from '../hooks/usePlaybackClock';
import { pointAtTime, totalDuration } from '../utils/trajectory';
import { encodeGuid } from '../utils/encodeGuid';

/**
 * Track map + drive playback (/F80/, /U20/, builds on 4.3): draws the run's 2D path and
 * animates a heading-oriented marker along it. Pure display — all values from GraphQL.
 */
export function TrajectoryPage() {
    const { t } = useTranslation();
    const { deviceGuid = '', runId = '' } = useParams<{ deviceGuid: string; runId: string }>();

    const { data: points, isPending, isError } = useTrajectory(runId);
    const duration = useMemo(() => totalDuration(points ?? []), [points]);
    const clock = usePlaybackClock(duration);
    const active = useMemo(() => pointAtTime(points ?? [], clock.time), [points, clock.time]);

    return (
        <PageShell title={t('trajectory.title')}>
            <Link
                to={`/vehicles/${encodeGuid(deviceGuid)}/runs/${encodeURIComponent(runId)}`}
                className="mb-2 inline-flex items-center gap-1 text-sm font-medium text-f1-red transition-colors hover:text-f1-red-hi hover:underline"
            >
                <ChevronLeft className="h-4 w-4" aria-hidden="true" />
                {t('trajectory.backToRun')}
            </Link>
            <p className="mb-4 font-mono text-xs text-slate-400 dark:text-slate-500">{runId}</p>

            {isPending && <p className="text-slate-500">{t('trajectory.loading')}</p>}
            {isError && (
                <p role="alert" className="text-red-600 dark:text-red-400">
                    {t('trajectory.loadFailed')}
                </p>
            )}
            {points && points.length === 0 && (
                <p className="text-slate-500 dark:text-slate-400">{t('trajectory.empty')}</p>
            )}
            {points && points.length > 0 && (
                <>
                    <TrajectoryMap points={points} active={active} />
                    <PlaybackControls clock={clock} duration={duration} />
                </>
            )}
        </PageShell>
    );
}
