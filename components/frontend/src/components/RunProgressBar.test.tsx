import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RunProgressBar } from './RunProgressBar';
import { useRunProgress } from '../hooks/useRunProgress';
import type { RunProgressUpdate } from '../models/realtime';

vi.mock('../hooks/useRunProgress', () => ({
    useRunProgress: vi.fn(),
}));

const useRunProgressMock = vi.mocked(useRunProgress);

function progress(sampledCount: number, totalSamples: number): RunProgressUpdate {
    return {
        deviceGuid: 'GUID-A',
        sampledCount,
        totalSamples,
        observedAtUtc: '2026-06-07T12:00:00Z',
    };
}

describe('RunProgressBar', () => {
    it('renders nothing before the first progress event', () => {
        useRunProgressMock.mockReturnValueOnce(null);

        const { container } = render(<RunProgressBar deviceGuid="GUID-A" />);

        expect(container).toBeEmptyDOMElement();
    });

    it('renders nothing for a zero-total run (no division by zero)', () => {
        useRunProgressMock.mockReturnValueOnce(progress(0, 0));

        const { container } = render(<RunProgressBar deviceGuid="GUID-A" />);

        expect(container).toBeEmptyDOMElement();
    });

    it('renders the floored percentage and counts', () => {
        useRunProgressMock.mockReturnValueOnce(progress(500, 1000));

        render(<RunProgressBar deviceGuid="GUID-A" />);

        expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '50');
        expect(screen.getByText('500 / 1000 samples (50 %)')).toBeInTheDocument();
    });

    it('shows 100 % only at actual completion (999/1000 floors to 99)', () => {
        useRunProgressMock.mockReturnValueOnce(progress(999, 1000));

        render(<RunProgressBar deviceGuid="GUID-A" />);

        expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '99');
    });

    it('clamps an overflowing count to the total', () => {
        useRunProgressMock.mockReturnValueOnce(progress(1100, 1000));

        render(<RunProgressBar deviceGuid="GUID-A" />);

        expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '100');
        expect(screen.getByText('1000 / 1000 samples (100 %)')).toBeInTheDocument();
    });
});
