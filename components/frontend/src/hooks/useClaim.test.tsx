import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren } from 'react';
import { useClaim } from './useClaim';
import { vehiclesQueryKey } from './useVehicles';
import { vehicleService } from '../services/vehicleService';
import type { VehicleResponse } from '../models/api';

vi.mock('../services/vehicleService', () => ({
    vehicleService: { list: vi.fn(), claim: vi.fn() },
}));

const claimMock = vi.mocked(vehicleService.claim);

const claimed: VehicleResponse = {
    deviceGuid: 'GUID-A',
    name: 'kart-1',
    owner: 'admin',
    registrationStatus: 'registered',
    createdAt: '2026-06-07T10:00:00Z',
    metadata: {},
};

function setup() {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const wrapper = ({ children }: PropsWithChildren) => (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    return { invalidate, wrapper };
}

describe('useClaim', () => {
    it('claims via the service and invalidates the vehicle list on success', async () => {
        claimMock.mockResolvedValueOnce(claimed);
        const { invalidate, wrapper } = setup();
        const { result } = renderHook(() => useClaim(), { wrapper });

        result.current.mutate({ deviceGuid: 'GUID-A', request: { name: 'kart-1' } });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(claimMock).toHaveBeenCalledWith('GUID-A', { name: 'kart-1' });
        expect(invalidate).toHaveBeenCalledWith({ queryKey: vehiclesQueryKey });
    });

    it('surfaces failures without invalidating', async () => {
        claimMock.mockRejectedValueOnce(new Error('boom'));
        const { invalidate, wrapper } = setup();
        const { result } = renderHook(() => useClaim(), { wrapper });

        result.current.mutate({ deviceGuid: 'GUID-A', request: { name: 'kart-1' } });

        await waitFor(() => expect(result.current.isError).toBe(true));
        expect(invalidate).not.toHaveBeenCalled();
    });
});
