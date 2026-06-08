import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren } from 'react';
import { useVehicles } from './useVehicles';
import { vehicleService } from '../services/vehicleService';
import type { VehicleResponse } from '../models/api';

vi.mock('../services/vehicleService', () => ({
    vehicleService: { list: vi.fn() },
}));

const listMock = vi.mocked(vehicleService.list);

function wrapper({ children }: PropsWithChildren) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('useVehicles', () => {
    it('returns the vehicle list from the service', async () => {
        const vehicles: VehicleResponse[] = [
            {
                deviceGuid: 'GUID-A',
                name: 'pending device GUID-A',
                owner: '',
                registrationStatus: 'pending',
                createdAt: '2026-06-07T10:00:00Z',
                metadata: {},
            },
        ];
        listMock.mockResolvedValueOnce(vehicles);

        const { result } = renderHook(() => useVehicles(), { wrapper });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(result.current.data).toEqual(vehicles);
    });

    it('exposes the error state when the service fails', async () => {
        listMock.mockRejectedValueOnce(new Error('boom'));

        const { result } = renderHook(() => useVehicles(), { wrapper });

        await waitFor(() => expect(result.current.isError).toBe(true));
    });
});
