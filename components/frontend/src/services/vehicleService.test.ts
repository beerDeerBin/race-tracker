import { describe, expect, it, vi } from 'vitest';
import { AxiosError } from 'axios';
import { DeviceNotFoundError, vehicleService } from './vehicleService';
import { httpClient } from '../utils/httpClient';
import { config } from '../utils/config';
import type { VehicleResponse } from '../models/api';

const claimed: VehicleResponse = {
    deviceGuid: 'GUID-Aa',
    name: 'kart-1',
    owner: 'admin',
    registrationStatus: 'registered',
    createdAt: '2026-06-07T10:00:00Z',
    metadata: {},
};

describe('vehicleService.claim', () => {
    it('posts to the encoded, case-preserved claim route with the request body', async () => {
        const post = vi.spyOn(httpClient, 'post').mockResolvedValueOnce({ data: claimed });

        const result = await vehicleService.claim('GUID-Aa', { name: 'kart-1' });

        expect(post).toHaveBeenCalledWith(`${config.managementUrl}/vehicles/GUID-Aa/claim`, {
            name: 'kart-1',
        });
        expect(result).toEqual(claimed);
    });

    it('maps a 404 to DeviceNotFoundError', async () => {
        const notFound = new AxiosError('Not Found');
        notFound.response = { status: 404 } as AxiosError['response'];
        vi.spyOn(httpClient, 'post').mockRejectedValueOnce(notFound);

        await expect(vehicleService.claim('GUID-X', { name: 'x' })).rejects.toBeInstanceOf(
            DeviceNotFoundError,
        );
    });

    it('rethrows non-404 failures untouched', async () => {
        const failure = new Error('boom');
        vi.spyOn(httpClient, 'post').mockRejectedValueOnce(failure);

        await expect(vehicleService.claim('GUID-X', { name: 'x' })).rejects.toBe(failure);
    });
});
