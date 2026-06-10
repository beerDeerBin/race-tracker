import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { ReactElement } from 'react';
import { Tabs } from './Tabs';

const tabs = [
    { id: 'runs', label: 'Runs', panel: <p>runs panel</p> },
    { id: 'gallery', label: 'Gallery', panel: <p>gallery panel</p> },
];

function renderAt(initial: string, ui: ReactElement) {
    return render(<MemoryRouter initialEntries={[initial]}>{ui}</MemoryRouter>);
}

describe('Tabs', () => {
    it('shows the first tab by default and only its panel', () => {
        renderAt('/', <Tabs tabs={tabs} />);

        expect(screen.getByRole('tab', { name: 'Runs' })).toHaveAttribute('aria-selected', 'true');
        expect(screen.getByText('runs panel')).toBeInTheDocument();
        expect(screen.queryByText('gallery panel')).not.toBeInTheDocument();
    });

    it('activates the tab named by the ?tab query param', () => {
        renderAt('/?tab=gallery', <Tabs tabs={tabs} />);

        expect(screen.getByRole('tab', { name: 'Gallery' })).toHaveAttribute(
            'aria-selected',
            'true',
        );
        expect(screen.getByText('gallery panel')).toBeInTheDocument();
    });

    it('switches panels on click', async () => {
        renderAt('/', <Tabs tabs={tabs} />);

        await userEvent.click(screen.getByRole('tab', { name: 'Gallery' }));

        expect(screen.getByText('gallery panel')).toBeInTheDocument();
        expect(screen.queryByText('runs panel')).not.toBeInTheDocument();
    });

    it('moves between tabs with the arrow keys', async () => {
        renderAt('/', <Tabs tabs={tabs} />);

        screen.getByRole('tab', { name: 'Runs' }).focus();
        await userEvent.keyboard('{ArrowRight}');

        expect(screen.getByRole('tab', { name: 'Gallery' })).toHaveAttribute(
            'aria-selected',
            'true',
        );
        await userEvent.keyboard('{ArrowRight}');
        // Wraps back to the first tab.
        expect(screen.getByRole('tab', { name: 'Runs' })).toHaveAttribute('aria-selected', 'true');
    });
});
