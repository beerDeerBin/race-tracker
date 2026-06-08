import { describe, expect, it, vi } from 'vitest';
import { rollupService } from './rollupService';
import * as graphqlClientModule from '../utils/graphqlClient';

describe('rollupService.getRunRollup', () => {
    it('queries runRollup(runId) with the page-clamp limit and returns the buckets', async () => {
        const buckets = [{ bucketStartIndex: 0, sampleCount: 100 }];
        const request = vi
            .spyOn(graphqlClientModule, 'graphqlRequest')
            .mockResolvedValueOnce({ runRollup: buckets });

        const result = await rollupService.getRunRollup('run-1');

        expect(request).toHaveBeenCalledWith(expect.stringContaining('runRollup(runId:'), {
            runId: 'run-1',
            limit: 50_000,
        });
        expect(result).toEqual(buckets);
    });
});
