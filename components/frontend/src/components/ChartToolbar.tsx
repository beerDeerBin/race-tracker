import { useTranslation } from 'react-i18next';

/**
 * Diagram filter controls (/F82/, /F53/): raw↔aggregate view toggle, inclusive time
 * range in seconds (blank = unbounded), per-axis visibility. Fully controlled — the run
 * detail page owns the state.
 */

export type ChartView = 'raw' | 'aggregate';

export interface AxisVisibility {
    x: boolean;
    y: boolean;
    z: boolean;
}

function parseSeconds(raw: string): number | null {
    if (raw.trim() === '') {
        return null;
    }
    const value = Number(raw);
    if (!Number.isFinite(value)) {
        return null;
    }
    // Negative bounds clamp to 0 instead of silently becoming "unbounded".
    return Math.max(0, value);
}

export function ChartToolbar({
    view,
    onViewChange,
    fromSeconds,
    toSeconds,
    onRangeChange,
    axes,
    onAxesChange,
}: {
    view: ChartView;
    onViewChange: (view: ChartView) => void;
    fromSeconds: number | null;
    toSeconds: number | null;
    onRangeChange: (fromSeconds: number | null, toSeconds: number | null) => void;
    axes: AxisVisibility;
    onAxesChange: (axes: AxisVisibility) => void;
}) {
    const { t } = useTranslation();

    const viewButton = (value: ChartView, labelKey: string) => (
        <button
            type="button"
            onClick={() => onViewChange(value)}
            aria-pressed={view === value}
            className={`rounded px-3 py-1 text-xs font-medium transition-colors ${
                view === value
                    ? 'bg-f1-red text-white'
                    : 'border border-slate-300 text-slate-700 hover:border-f1-red hover:text-f1-red dark:border-slate-700 dark:text-slate-300'
            }`}
        >
            {t(labelKey)}
        </button>
    );

    const axisCheckbox = (axis: keyof AxisVisibility) => (
        <label className="flex items-center gap-1 text-sm text-slate-700 dark:text-slate-300">
            <input
                type="checkbox"
                checked={axes[axis]}
                onChange={(event) => onAxesChange({ ...axes, [axis]: event.target.checked })}
                className="accent-f1-red"
            />
            {axis}
        </label>
    );

    const rangeInputClasses =
        'w-20 rounded border border-slate-300 bg-white px-2 py-1 text-sm text-slate-900 transition-colors outline-none focus:border-f1-red focus:ring-1 focus:ring-f1-red dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100';

    return (
        <div className="mb-4 flex flex-wrap items-center gap-x-6 gap-y-3 rounded-lg border border-slate-200 bg-white p-3 dark:border-slate-800 dark:bg-slate-900">
            <div className="flex items-center gap-1.5">
                <span className="text-sm text-slate-500 dark:text-slate-400">
                    {t('chartToolbar.view')}
                </span>
                {viewButton('raw', 'chartToolbar.raw')}
                {viewButton('aggregate', 'chartToolbar.aggregate')}
            </div>

            <div className="flex items-center gap-1.5">
                <label className="text-sm text-slate-500 dark:text-slate-400">
                    {t('chartToolbar.from')}
                    <input
                        type="number"
                        min={0}
                        step="any"
                        value={fromSeconds ?? ''}
                        onChange={(event) =>
                            onRangeChange(parseSeconds(event.target.value), toSeconds)
                        }
                        className={`ml-1.5 ${rangeInputClasses}`}
                    />
                </label>
                <label className="text-sm text-slate-500 dark:text-slate-400">
                    {t('chartToolbar.to')}
                    <input
                        type="number"
                        min={0}
                        step="any"
                        value={toSeconds ?? ''}
                        onChange={(event) =>
                            onRangeChange(fromSeconds, parseSeconds(event.target.value))
                        }
                        className={`ml-1.5 ${rangeInputClasses}`}
                    />
                </label>
                {(fromSeconds !== null || toSeconds !== null) && (
                    <button
                        type="button"
                        onClick={() => onRangeChange(null, null)}
                        className="rounded border border-slate-300 px-2 py-1 text-xs text-slate-700 transition-colors hover:border-f1-red hover:text-f1-red dark:border-slate-700 dark:text-slate-300"
                    >
                        {t('chartToolbar.reset')}
                    </button>
                )}
            </div>

            <div className="flex items-center gap-3">
                <span className="text-sm text-slate-500 dark:text-slate-400">
                    {t('chartToolbar.axes')}
                </span>
                {axisCheckbox('x')}
                {axisCheckbox('y')}
                {axisCheckbox('z')}
            </div>
        </div>
    );
}
