import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren } from 'react';
import { useCommands } from './useCommands';
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

function wrapper({ children }: PropsWithChildren) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('useCommands', () => {
    it('dispatches the simple commands for the given device', async () => {
        serviceMock.connect.mockResolvedValueOnce(undefined);
        serviceMock.disconnect.mockResolvedValueOnce(undefined);
        serviceMock.reset.mockResolvedValueOnce(undefined);
        const { result } = renderHook(() => useCommands('GUID-A'), { wrapper });

        result.current.connect.mutate();
        result.current.disconnect.mutate();
        result.current.reset.mutate();

        await waitFor(() => expect(serviceMock.connect).toHaveBeenCalledWith('GUID-A'));
        await waitFor(() => expect(serviceMock.disconnect).toHaveBeenCalledWith('GUID-A'));
        await waitFor(() => expect(serviceMock.reset).toHaveBeenCalledWith('GUID-A'));
    });

    it('startRun forwards the parameters and resolves the runId', async () => {
        serviceMock.startRun.mockResolvedValueOnce({ runId: 'run-1' });
        const { result } = renderHook(() => useCommands('GUID-A'), { wrapper });

        result.current.startRun.mutate({ numSamples: 500 });

        await waitFor(() => expect(result.current.startRun.isSuccess).toBe(true));
        expect(serviceMock.startRun).toHaveBeenCalledWith('GUID-A', { numSamples: 500 });
        expect(result.current.startRun.data?.runId).toBe('run-1');
    });
});
