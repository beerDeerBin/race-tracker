import { useQuery } from '@tanstack/react-query';
import { imageService } from '../services/imageService';

/** Query key for a vehicle's gallery image list. */
export const vehicleImagesQueryKey = (deviceGuid: string) =>
    ['vehicle-images', deviceGuid] as const;

/** A vehicle's gallery images — server state via React Query over the image service. */
export function useVehicleImages(deviceGuid: string) {
    return useQuery({
        queryKey: vehicleImagesQueryKey(deviceGuid),
        queryFn: () => imageService.list(deviceGuid),
    });
}
