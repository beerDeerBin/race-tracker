import { useRef, type ReactNode } from 'react';
import { useSearchParams } from 'react-router-dom';

export interface TabDef {
    /** Stable id, also written to the `?tab=` query param. */
    id: string;
    label: string;
    panel: ReactNode;
}

/**
 * Minimal accessible tablist (WAI-ARIA: roving tabindex, ArrowLeft/Right/Home/End auto-activation,
 * aria-selected/-controls). The active tab is synced to a search param so it is linkable and survives
 * a reload; only the active panel is mounted (so e.g. the gallery only fetches when opened).
 */
export function Tabs({ tabs, paramKey = 'tab' }: { tabs: TabDef[]; paramKey?: string }) {
    const [searchParams, setSearchParams] = useSearchParams();
    const refs = useRef<(HTMLButtonElement | null)[]>([]);

    const requested = searchParams.get(paramKey);
    const active = tabs.find((tab) => tab.id === requested) ?? tabs[0];
    if (!active) {
        return null;
    }

    const select = (id: string) =>
        setSearchParams(
            (prev) => {
                const next = new URLSearchParams(prev);
                next.set(paramKey, id);
                return next;
            },
            { replace: true },
        );

    const onKeyDown = (event: React.KeyboardEvent, index: number) => {
        const last = tabs.length - 1;
        let target: number | null = null;
        if (event.key === 'ArrowRight') target = index === last ? 0 : index + 1;
        else if (event.key === 'ArrowLeft') target = index === 0 ? last : index - 1;
        else if (event.key === 'Home') target = 0;
        else if (event.key === 'End') target = last;
        if (target === null) return;
        const targetTab = tabs[target];
        if (!targetTab) return;
        event.preventDefault();
        refs.current[target]?.focus();
        select(targetTab.id);
    };

    return (
        <div>
            <div
                role="tablist"
                className="mb-4 flex gap-1 border-b border-slate-200 dark:border-slate-800"
            >
                {tabs.map((tab, index) => {
                    const selected = tab.id === active.id;
                    return (
                        <button
                            key={tab.id}
                            ref={(element) => {
                                refs.current[index] = element;
                            }}
                            type="button"
                            role="tab"
                            id={`tab-${tab.id}`}
                            aria-selected={selected}
                            aria-controls={`panel-${tab.id}`}
                            tabIndex={selected ? 0 : -1}
                            onClick={() => select(tab.id)}
                            onKeyDown={(event) => onKeyDown(event, index)}
                            className={`-mb-px border-b-2 px-4 py-2 text-sm font-medium transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-f1-red ${
                                selected
                                    ? 'border-f1-red text-f1-red'
                                    : 'border-transparent text-slate-500 hover:text-f1-red dark:text-slate-400'
                            }`}
                        >
                            {tab.label}
                        </button>
                    );
                })}
            </div>
            {tabs.map((tab) => (
                <div
                    key={tab.id}
                    role="tabpanel"
                    id={`panel-${tab.id}`}
                    aria-labelledby={`tab-${tab.id}`}
                    hidden={tab.id !== active.id}
                >
                    {tab.id === active.id && tab.panel}
                </div>
            ))}
        </div>
    );
}
