import { describe, expect, it, vi } from 'vitest';
import { trajectoryService } from './trajectoryService';
import * as graphqlClientModule from '../utils/graphqlClient';

describe('trajectoryService.getTrajectory', () => {
    it('queries trajectory(runId) with the limit and a null stride by default', async () => {
        const points = [{ index: 0, t: 0, x: 0, y: 0, heading: 0 }];
        const request = vi
            .spyOn(graphqlClientModule, 'graphqlRequest')
            .mockResolvedValueOnce({ trajectory: points });

        const result = await trajectoryService.getTrajectory('run-1');

        expect(request).toHaveBeenCalledWith(expect.stringContaining('trajectory(runId:'), {
            runId: 'run-1',
            stride: null,
            limit: 100_000,
        });
        expect(result).toEqual(points);
    });

    it('passes a stride through for downsampling', async () => {
        const request = vi
            .spyOn(graphqlClientModule, 'graphqlRequest')
            .mockResolvedValueOnce({ trajectory: [] });

        await trajectoryService.getTrajectory('run-1', 10);

        expect(request).toHaveBeenCalledWith(expect.any(String), {
            runId: 'run-1',
            stride: 10,
            limit: 100_000,
        });
    });
});
