import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren } from 'react';
import { useRuns } from './useRuns';
import { runService } from '../services/runService';
import type { Run } from '../models/graphql';

vi.mock('../services/runService', () => ({
    runService: { getRuns: vi.fn() },
}));

const getRunsMock = vi.mocked(runService.getRuns);

function run(runId: string, startedAt: string | null): Run {
    return {
        deviceGuid: 'GUID-A',
        runId,
        numSamples: null,
        odrHz: null,
        accelRange: null,
        gyroRange: null,
        startedAt,
        endedAt: null,
        receivedSamples: 0,
    };
}

function wrapper({ children }: PropsWithChildren) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('useRuns', () => {
    it('sorts runs newest first (unknown start times last)', async () => {
        getRunsMock.mockResolvedValueOnce([
            run('old', '2026-06-07T10:00:00Z'),
            run('unknown', null),
            run('new', '2026-06-07T17:00:00Z'),
        ]);

        const { result } = renderHook(() => useRuns('GUID-A'), { wrapper });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(result.current.data?.map((r) => r.runId)).toEqual(['new', 'old', 'unknown']);
    });
});
