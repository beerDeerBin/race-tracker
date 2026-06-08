import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { sampleService } from './sampleService';
import * as graphqlClientModule from '../utils/graphqlClient';
import type { Sample } from '../models/graphql';

function page(startIndex: number, count: number): Sample[] {
    return Array.from({ length: count }, (_, i) => ({
        index: startIndex + i,
        ax: 0,
        ay: 0,
        az: 9.81,
        gx: 0,
        gy: 0,
        gz: 0,
    }));
}

describe('sampleService.getAllSamples', () => {
    beforeEach(() => vi.restoreAllMocks());
    afterEach(() => vi.restoreAllMocks());

    it('returns a single short page directly', async () => {
        const request = vi
            .spyOn(graphqlClientModule, 'graphqlRequest')
            .mockResolvedValueOnce({ samples: page(0, 500) });

        const all = await sampleService.getAllSamples('run-1');

        expect(all).toHaveLength(500);
        expect(request).toHaveBeenCalledTimes(1);
        expect(request).toHaveBeenCalledWith(expect.stringContaining('samples('), {
            runId: 'run-1',
            fromIndex: 0,
            limit: 50_000,
        });
    });

    it('pages through full pages, advancing fromIndex past the last received index', async () => {
        const request = vi
            .spyOn(graphqlClientModule, 'graphqlRequest')
            .mockResolvedValueOnce({ samples: page(0, 50_000) })
            .mockResolvedValueOnce({ samples: page(50_000, 1_000) });

        const all = await sampleService.getAllSamples('run-1');

        expect(all).toHaveLength(51_000);
        expect(request).toHaveBeenNthCalledWith(2, expect.any(String), {
            runId: 'run-1',
            fromIndex: 50_000,
            limit: 50_000,
        });
        // No duplicates across the page boundary.
        expect(all[49_999]!.index).toBe(49_999);
        expect(all[50_000]!.index).toBe(50_000);
    });

    it('terminates even if a misbehaving server resends the same full page', async () => {
        // fromIndex advances past the last received index regardless of page content,
        // and the cap bounds total work — no infinite loop.
        const request = vi
            .spyOn(graphqlClientModule, 'graphqlRequest')
            .mockResolvedValue({ samples: page(0, 50_000) });

        const all = await sampleService.getAllSamples('run-stuck');

        expect(all.length).toBeLessThanOrEqual(200_000);
        expect(request.mock.calls.length).toBeLessThanOrEqual(4);
        // fromIndex advanced monotonically across calls.
        const fromIndexes = request.mock.calls.map(
            (call) => (call[1] as { fromIndex: number }).fromIndex,
        );
        expect(fromIndexes).toEqual([...fromIndexes].sort((a, b) => a - b));
    });

    it('stops at the in-memory cap on huge runs', async () => {
        const request = vi
            .spyOn(graphqlClientModule, 'graphqlRequest')
            .mockResolvedValue({ samples: page(0, 50_000) });

        const all = await sampleService.getAllSamples('run-huge');

        expect(all).toHaveLength(200_000);
        expect(request).toHaveBeenCalledTimes(4);
    });
});
