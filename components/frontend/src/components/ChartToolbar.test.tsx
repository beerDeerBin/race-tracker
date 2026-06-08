import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ChartToolbar } from './ChartToolbar';
import type { AxisVisibility, ChartView } from './ChartToolbar';

function renderToolbar(overrides?: {
    view?: ChartView;
    fromSeconds?: number | null;
    toSeconds?: number | null;
    axes?: AxisVisibility;
}) {
    const onViewChange = vi.fn();
    const onRangeChange = vi.fn();
    const onAxesChange = vi.fn();
    render(
        <ChartToolbar
            view={overrides?.view ?? 'raw'}
            onViewChange={onViewChange}
            fromSeconds={overrides?.fromSeconds ?? null}
            toSeconds={overrides?.toSeconds ?? null}
            onRangeChange={onRangeChange}
            axes={overrides?.axes ?? { x: true, y: true, z: true }}
            onAxesChange={onAxesChange}
        />,
    );
    return { onViewChange, onRangeChange, onAxesChange };
}

describe('ChartToolbar', () => {
    it('switches the view', async () => {
        const { onViewChange } = renderToolbar();

        await userEvent.click(screen.getByRole('button', { name: 'Aggregate' }));

        expect(onViewChange).toHaveBeenCalledWith('aggregate');
    });

    it('emits parsed range bounds and null for cleared inputs', async () => {
        const { onRangeChange } = renderToolbar();

        await userEvent.type(screen.getByLabelText('From [s]'), '2');

        expect(onRangeChange).toHaveBeenLastCalledWith(2, null);
    });

    it('clamps negative bounds to zero instead of treating them as unbounded', async () => {
        const { onRangeChange } = renderToolbar();

        await userEvent.type(screen.getByLabelText('From [s]'), '-3');

        expect(onRangeChange).toHaveBeenLastCalledWith(0, null);
    });

    it('resets the range', async () => {
        const { onRangeChange } = renderToolbar({ fromSeconds: 1, toSeconds: 3 });

        await userEvent.click(screen.getByRole('button', { name: 'Reset' }));

        expect(onRangeChange).toHaveBeenCalledWith(null, null);
    });

    it('toggles a single axis', async () => {
        const { onAxesChange } = renderToolbar();

        await userEvent.click(screen.getByRole('checkbox', { name: 'y' }));

        expect(onAxesChange).toHaveBeenCalledWith({ x: true, y: false, z: true });
    });
});
