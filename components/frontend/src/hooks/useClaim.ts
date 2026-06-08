import { useMutation, useQueryClient } from '@tanstack/react-query';
import { vehicleService } from '../services/vehicleService';
import { vehiclesQueryKey } from './useVehicles';
import type { ClaimVehicleRequest } from '../models/api';

/**
 * Claims a pending device (/F25/). On success the vehicle list query is invalidated so
 * the dashboard reflects the new registration without a reload.
 */
export function useClaim() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({
            deviceGuid,
            request,
        }: {
            deviceGuid: string;
            request: ClaimVehicleRequest;
        }) => vehicleService.claim(deviceGuid, request),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: vehiclesQueryKey }),
    });
}
