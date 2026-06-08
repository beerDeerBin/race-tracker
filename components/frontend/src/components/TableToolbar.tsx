import type { ReactNode } from 'react';
import { Search } from 'lucide-react';

/**
 * Shared search + filter bar above a data table. A controlled search box (left) plus optional
 * filter/sort controls passed as `children` (right). Client-side — the tables filter their own
 * already-loaded arrays.
 */
export function TableToolbar({
    value,
    onChange,
    placeholder,
    children,
}: {
    value: string;
    onChange: (value: string) => void;
    placeholder: string;
    children?: ReactNode;
}) {
    return (
        <div className="mb-3 flex flex-wrap items-center gap-2">
            <div className="relative min-w-48 flex-1">
                <Search
                    className="pointer-events-none absolute top-1/2 left-2.5 h-4 w-4 -translate-y-1/2 text-slate-400"
                    aria-hidden="true"
                />
                <input
                    type="search"
                    value={value}
                    onChange={(event) => onChange(event.target.value)}
                    placeholder={placeholder}
                    aria-label={placeholder}
                    className="field w-full pl-8"
                />
            </div>
            {children}
        </div>
    );
}
