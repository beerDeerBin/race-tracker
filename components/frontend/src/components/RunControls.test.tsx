import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren } from 'react';
import { RunControls } from './RunControls';
import { commandService } from '../services/commandService';

vi.mock('../services/commandService', () => ({
    commandService: {
        connect: vi.fn(),
        startRun: vi.fn(),
        disconnect: vi.fn(),
        reset: vi.fn(),
    },
}));

const serviceMock = vi.mocked(commandService);

function Providers({ children }: PropsWithChildren) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

function renderControls(state: 'Idle' | 'Connected' | 'Acquiring' | null) {
    return render(<RunControls deviceGuid="GUID-A" state={state} />, { wrapper: Providers });
}

describe('RunControls (/F34/)', () => {
    beforeEach(() => {
        serviceMock.connect.mockReset();
        serviceMock.startRun.mockReset();
    });

    it('enables only Connect and Reset while Idle, with reasons on the rest', () => {
        renderControls('Idle');

        expect(screen.getByRole('button', { name: 'Connect' })).toBeEnabled();
        expect(screen.getByRole('button', { name: 'Reset' })).toBeEnabled();
        const startRun = screen.getByRole('button', { name: 'Start run' });
        const disconnect = screen.getByRole('button', { name: 'Disconnect' });
        expect(startRun).toBeDisabled();
        expect(disconnect).toBeDisabled();
        expect(startRun).toHaveAttribute('title', 'Only available while the device is connected.');
    });

    it('enables Start run, Disconnect and Reset while Connected', () => {
        renderControls('Connected');

        expect(screen.getByRole('button', { name: 'Start run' })).toBeEnabled();
        expect(screen.getByRole('button', { name: 'Disconnect' })).toBeEnabled();
        expect(screen.getByRole('button', { name: 'Reset' })).toBeEnabled();
        expect(screen.getByRole('button', { name: 'Connect' })).toBeDisabled();
    });

    it('disables everything while Acquiring, explaining the running measurement', () => {
        renderControls('Acquiring');

        for (const name of ['Connect', 'Start run', 'Disconnect', 'Reset']) {
            const button = screen.getByRole('button', { name });
            expect(button).toBeDisabled();
            expect(button).toHaveAttribute(
                'title',
                'A run is in progress — no commands are accepted while acquiring.',
            );
        }
    });

    it('disables everything without a live status', () => {
        renderControls(null);

        for (const name of ['Connect', 'Start run', 'Disconnect', 'Reset']) {
            expect(screen.getByRole('button', { name })).toBeDisabled();
        }
    });

    it('dispatches connect on click', async () => {
        serviceMock.connect.mockResolvedValueOnce(undefined);
        renderControls('Idle');

        await userEvent.click(screen.getByRole('button', { name: 'Connect' }));

        await waitFor(() => expect(serviceMock.connect).toHaveBeenCalledWith('GUID-A'));
    });

    it('starts a run with the dialog defaults', async () => {
        serviceMock.startRun.mockResolvedValueOnce({ runId: 'run-1' });
        renderControls('Connected');

        await userEvent.click(screen.getByRole('button', { name: 'Start run' }));
        await userEvent.click(screen.getByRole('button', { name: 'Start' }));

        await waitFor(() =>
            expect(serviceMock.startRun).toHaveBeenCalledWith('GUID-A', {
                numSamples: 1000,
                odr: 'hz104',
                accelRange: 'g4',
                gyroRange: 'dps500',
            }),
        );
        // Dialog closes on success.
        await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    });

    it('starts a run with customized parameters', async () => {
        serviceMock.startRun.mockResolvedValueOnce({ runId: 'run-2' });
        renderControls('Connected');

        await userEvent.click(screen.getByRole('button', { name: 'Start run' }));
        const samples = screen.getByLabelText('Number of samples');
        await userEvent.clear(samples);
        await userEvent.type(samples, '500');
        await userEvent.selectOptions(screen.getByLabelText('Sample rate (ODR)'), 'hz208');
        await userEvent.selectOptions(screen.getByLabelText('Accelerometer range'), 'g8');
        await userEvent.click(screen.getByRole('button', { name: 'Start' }));

        await waitFor(() =>
            expect(serviceMock.startRun).toHaveBeenCalledWith('GUID-A', {
                numSamples: 500,
                odr: 'hz208',
                accelRange: 'g8',
                gyroRange: 'dps500',
            }),
        );
    });

    it('never submits an invalid sample count', async () => {
        renderControls('Connected');

        await userEvent.click(screen.getByRole('button', { name: 'Start run' }));
        const samples = screen.getByLabelText('Number of samples');
        await userEvent.clear(samples);
        await userEvent.type(samples, '0');
        await userEvent.click(screen.getByRole('button', { name: 'Start' }));

        expect(serviceMock.startRun).not.toHaveBeenCalled();
    });
});
