import { useQuery } from '@tanstack/react-query';
import { vehicleService } from '../services/vehicleService';

export const vehiclesQueryKey = ['vehicles'] as const;

/** The dashboard's vehicle list (/F83/) — server state via React Query over the service. */
export function useVehicles() {
    return useQuery({
        queryKey: vehiclesQueryKey,
        queryFn: vehicleService.list,
    });
}
