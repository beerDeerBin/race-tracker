import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ChevronDown, ChevronRight, ChevronsUpDown, ChevronUp } from 'lucide-react';
import { encodeGuid } from '../utils/encodeGuid';
import { effectiveOdrHz } from '../utils/odr';
import { TableToolbar } from './TableToolbar';
import type { Run } from '../models/graphql';

type SortKey = 'startedAt' | 'samples' | 'odr';
type SortDir = 'asc' | 'desc';

/**
 * A vehicle's runs (/F80/); each row links to the six-axis detail. A search box (by runId) narrows
 * the list, and the Started / Samples / Sample-rate column headers sort on click (Run-ID and the
 * Open action are not sortable). Runs are static data, so sorting never fights live updates.
 *
 * Links are built from the verbatim `deviceGuid` the page was reached with (the case-sensitive
 * cross-service key, CONVENTIONS §8) — **not** from `run.deviceGuid`, which comes from the
 * persistence GraphQL `uuid` column and is lower-cased. Routing the verbatim casing through keeps
 * the run-detail SignalR group/filter (7.7 live tail) matching the realtime service's verbatim group.
 */
export function RunList({ deviceGuid, runs }: { deviceGuid: string; runs: Run[] }) {
    const { t } = useTranslation();
    const [query, setQuery] = useState('');
    const [sort, setSort] = useState<{ key: SortKey; dir: SortDir }>({
        key: 'startedAt',
        dir: 'desc',
    });

    const onSort = (key: SortKey) =>
        setSort((s) =>
            s.key === key ? { key, dir: s.dir === 'asc' ? 'desc' : 'asc' } : { key, dir: 'asc' },
        );

    const visible = useMemo(() => {
        const q = query.trim().toLowerCase();
        const matched = q ? runs.filter((run) => run.runId.toLowerCase().includes(q)) : runs;
        const compare = (a: Run, b: Run): number => {
            switch (sort.key) {
                case 'startedAt':
                    return (
                        (a.startedAt ? Date.parse(a.startedAt) : 0) -
                        (b.startedAt ? Date.parse(b.startedAt) : 0)
                    );
                case 'samples':
                    return a.receivedSamples - b.receivedSamples;
                case 'odr':
                    return (a.odrHz ?? -1) - (b.odrHz ?? -1);
            }
        };
        return [...matched].sort((a, b) => (sort.dir === 'asc' ? compare(a, b) : -compare(a, b)));
    }, [runs, query, sort]);

    const th = 'px-4 py-3 md:text-center';
    const td = 'block py-1 md:table-cell md:px-4 md:py-3 md:text-center';

    const sortableTh = (key: SortKey, label: string) => (
        <th
            className={th}
            aria-sort={
                sort.key === key ? (sort.dir === 'asc' ? 'ascending' : 'descending') : 'none'
            }
        >
            <button
                type="button"
                onClick={() => onSort(key)}
                aria-label={t('filters.sortBy', { column: label })}
                className="inline-flex items-center gap-1 uppercase transition-colors hover:text-f1-red"
            >
                {label}
                {sort.key !== key ? (
                    <ChevronsUpDown className="h-3.5 w-3.5 opacity-40" aria-hidden="true" />
                ) : sort.dir === 'asc' ? (
                    <ChevronUp className="h-3.5 w-3.5 text-f1-red" aria-hidden="true" />
                ) : (
                    <ChevronDown className="h-3.5 w-3.5 text-f1-red" aria-hidden="true" />
                )}
            </button>
        </th>
    );

    return (
        <div>
            <TableToolbar value={query} onChange={setQuery} placeholder={t('filters.searchRuns')} />

            <div className="md:overflow-x-auto md:rounded-lg md:border md:border-slate-200 md:bg-white md:shadow-sm md:dark:border-slate-800 md:dark:bg-slate-900">
                {/* On md+ a normal table; below md each row stacks into a card with the column
                    header shown inline as a per-cell label (the real <thead> is hidden). */}
                <table className="block w-full text-left text-sm md:table">
                    <thead className="hidden border-b border-slate-200 text-xs text-slate-500 uppercase md:table-header-group dark:border-slate-800 dark:text-slate-400">
                        <tr>
                            {sortableTh('startedAt', t('runs.startedAt'))}
                            {sortableTh('samples', t('runs.samples'))}
                            {sortableTh('odr', t('runs.odr'))}
                            <th className={th}>{t('runs.runId')}</th>
                            <th className={th} />
                        </tr>
                    </thead>
                    <tbody className="block md:table-row-group">
                        {visible.map((run) => (
                            <tr
                                key={run.runId}
                                className="mb-3 block rounded-lg border border-slate-200 bg-white p-3 transition-colors last:mb-0 md:mb-0 md:table-row md:rounded-none md:border-0 md:border-b md:border-slate-100 md:bg-transparent md:p-0 md:last:border-0 md:hover:bg-slate-50 dark:border-slate-800 dark:bg-slate-900 md:dark:bg-transparent md:dark:hover:bg-slate-800/40"
                            >
                                <td className={`${td} text-slate-700 dark:text-slate-300`}>
                                    <span className="mr-2 inline-block font-medium text-slate-500 uppercase md:hidden dark:text-slate-400">
                                        {t('runs.startedAt')}
                                    </span>
                                    {run.startedAt ? new Date(run.startedAt).toLocaleString() : '—'}
                                </td>
                                <td className={`${td} text-slate-700 dark:text-slate-300`}>
                                    <span className="mr-2 inline-block font-medium text-slate-500 uppercase md:hidden dark:text-slate-400">
                                        {t('runs.samples')}
                                    </span>
                                    {run.numSamples != null
                                        ? `${run.receivedSamples} / ${run.numSamples}`
                                        : run.receivedSamples}
                                </td>
                                <td className={`${td} text-slate-700 dark:text-slate-300`}>
                                    <span className="mr-2 inline-block font-medium text-slate-500 uppercase md:hidden dark:text-slate-400">
                                        {t('runs.odr')}
                                    </span>
                                    {run.odrHz != null
                                        ? `${run.odrHz} Hz`
                                        : t('runs.odrAssumed', { odr: effectiveOdrHz(run) })}
                                </td>
                                <td
                                    className={`${td} font-mono text-xs text-slate-400 md:whitespace-nowrap dark:text-slate-500`}
                                >
                                    <span className="mr-2 inline-block font-sans font-medium text-slate-500 uppercase md:hidden dark:text-slate-400">
                                        {t('runs.runId')}
                                    </span>
                                    {run.runId}
                                </td>
                                <td className="block pt-2 md:table-cell md:px-4 md:py-3 md:pt-3 md:text-center">
                                    <Link
                                        to={`/vehicles/${encodeGuid(deviceGuid)}/runs/${encodeURIComponent(run.runId)}`}
                                        className="inline-flex items-center gap-1 rounded bg-f1-red px-3 py-1 text-xs font-medium whitespace-nowrap text-white transition-colors hover:bg-f1-red-hi"
                                    >
                                        {t('runs.open')}
                                        <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
                                    </Link>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {visible.length === 0 && (
                    <p className="px-4 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                        {t('filters.noMatches')}
                    </p>
                )}
            </div>
        </div>
    );
}
