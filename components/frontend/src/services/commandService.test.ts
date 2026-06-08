import { describe, expect, it, vi } from 'vitest';
import { AxiosError } from 'axios';
import { commandService } from './commandService';
import { DeviceNotFoundError } from './vehicleService';
import { httpClient } from '../utils/httpClient';
import { config } from '../utils/config';

const base = `${config.managementUrl}/vehicles/GUID-Aa/commands`;

describe('commandService', () => {
    it.each([
        ['connect', () => commandService.connect('GUID-Aa')],
        ['disconnect', () => commandService.disconnect('GUID-Aa')],
        ['reset', () => commandService.reset('GUID-Aa')],
    ] as const)('%s posts to the encoded case-preserved route', async (route, call) => {
        const post = vi.spyOn(httpClient, 'post').mockResolvedValueOnce({ data: undefined });

        await call();

        expect(post).toHaveBeenCalledWith(`${base}/${route}`, undefined);
    });

    it('startRun posts the parameters and resolves the effective runId', async () => {
        const post = vi
            .spyOn(httpClient, 'post')
            .mockResolvedValueOnce({ data: { runId: 'generated-run-id' } });

        const result = await commandService.startRun('GUID-Aa', {
            numSamples: 500,
            odr: 'hz104',
            accelRange: 'g4',
            gyroRange: 'dps500',
        });

        expect(post).toHaveBeenCalledWith(`${base}/start-run`, {
            numSamples: 500,
            odr: 'hz104',
            accelRange: 'g4',
            gyroRange: 'dps500',
        });
        expect(result.runId).toBe('generated-run-id');
    });

    it('maps a 404 to DeviceNotFoundError', async () => {
        const notFound = new AxiosError('Not Found');
        notFound.response = { status: 404 } as AxiosError['response'];
        vi.spyOn(httpClient, 'post').mockRejectedValueOnce(notFound);

        await expect(commandService.connect('GUID-X')).rejects.toBeInstanceOf(DeviceNotFoundError);
    });
});
