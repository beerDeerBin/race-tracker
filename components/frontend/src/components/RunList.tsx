import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { encodeGuid } from '../utils/encodeGuid';
import { effectiveOdrHz } from '../utils/odr';
import type { Run } from '../models/graphql';

/** A vehicle's runs (/F80/), newest first; each row links to the six-axis detail. */
export function RunList({ runs }: { runs: Run[] }) {
    const { t } = useTranslation();

    return (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
            <table className="w-full text-left text-sm">
                <thead className="border-b border-slate-200 text-xs text-slate-500 uppercase dark:border-slate-800 dark:text-slate-400">
                    <tr>
                        <th className="px-4 py-3">{t('runs.startedAt')}</th>
                        <th className="px-4 py-3">{t('runs.samples')}</th>
                        <th className="px-4 py-3">{t('runs.odr')}</th>
                        <th className="px-4 py-3">{t('runs.runId')}</th>
                        <th className="px-4 py-3" />
                    </tr>
                </thead>
                <tbody>
                    {runs.map((run) => (
                        <tr
                            key={run.runId}
                            className="border-b border-slate-100 last:border-0 dark:border-slate-800"
                        >
                            <td className="px-4 py-3 text-slate-700 dark:text-slate-300">
                                {run.startedAt ? new Date(run.startedAt).toLocaleString() : '—'}
                            </td>
                            <td className="px-4 py-3 text-slate-700 dark:text-slate-300">
                                {run.numSamples != null
                                    ? `${run.receivedSamples} / ${run.numSamples}`
                                    : run.receivedSamples}
                            </td>
                            <td className="px-4 py-3 text-slate-700 dark:text-slate-300">
                                {run.odrHz != null
                                    ? `${run.odrHz} Hz`
                                    : t('runs.odrAssumed', { odr: effectiveOdrHz(run) })}
                            </td>
                            <td className="px-4 py-3 font-mono text-xs text-slate-400 dark:text-slate-500">
                                {run.runId}
                            </td>
                            <td className="px-4 py-3">
                                <Link
                                    to={`/vehicles/${encodeGuid(run.deviceGuid)}/runs/${encodeURIComponent(run.runId)}`}
                                    className="rounded bg-sky-600 px-3 py-1 text-xs font-medium text-white hover:bg-sky-500"
                                >
                                    {t('runs.open')}
                                </Link>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}
