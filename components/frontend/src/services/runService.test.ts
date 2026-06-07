import { describe, expect, it, vi } from 'vitest';
import { runService } from './runService';
import * as graphqlClientModule from '../utils/graphqlClient';
import type { Run } from '../models/graphql';

const run: Run = {
    deviceGuid: 'GUID-A',
    runId: 'run-1',
    numSamples: null,
    odrHz: null,
    accelRange: null,
    gyroRange: null,
    startedAt: '2026-06-07T17:28:09Z',
    endedAt: '2026-06-07T17:28:14Z',
    receivedSamples: 500,
};

describe('runService.getRuns', () => {
    it('queries runs(deviceGuid) and returns the run list', async () => {
        const request = vi
            .spyOn(graphqlClientModule, 'graphqlRequest')
            .mockResolvedValueOnce({ runs: [run] });

        const runs = await runService.getRuns('GUID-A');

        expect(request).toHaveBeenCalledWith(expect.stringContaining('runs(deviceGuid:'), {
            deviceGuid: 'GUID-A',
        });
        expect(runs).toEqual([run]);
    });
});
