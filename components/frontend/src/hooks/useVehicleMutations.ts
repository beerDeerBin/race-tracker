import { useMutation, useQueryClient } from '@tanstack/react-query';
import { vehicleService } from '../services/vehicleService';
import { vehiclesQueryKey } from './useVehicles';
import type { UpdateVehicleRequest } from '../models/api';

/** Renames / updates a vehicle; invalidates the dashboard list so the change shows without a reload. */
export function useUpdateVehicle(deviceGuid: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (request: UpdateVehicleRequest) => vehicleService.update(deviceGuid, request),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: vehiclesQueryKey }),
    });
}

/** Releases (un-claims) a vehicle back to pending; invalidates the dashboard list. */
export function useUnclaimVehicle(deviceGuid: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: () => vehicleService.unclaim(deviceGuid),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: vehiclesQueryKey }),
    });
}
