import { useMutation, useQueryClient } from '@tanstack/react-query';
import { imageService } from '../services/imageService';
import { vehicleImagesQueryKey } from './useVehicleImages';
import { vehiclesQueryKey } from './useVehicles';

/**
 * Mutations over a vehicle's gallery (upload / delete / set-title). All invalidate the gallery list
 * **and** the dashboard vehicle list, since the latter carries `titleImageId` for the avatar — so a
 * title change is reflected everywhere without a reload. The guid is fixed per hook instance.
 */
/**
 * Uploads one or more images. Files are uploaded **sequentially** so the server-side "first upload
 * becomes the title" rule is deterministic when several arrive at once. The list is invalidated once,
 * after all uploads settle.
 */
export function useUploadImage(deviceGuid: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (files: File[]) => {
            for (const file of files) {
                await imageService.upload(deviceGuid, file);
            }
        },
        onSuccess: () => invalidate(queryClient, deviceGuid),
    });
}

export function useDeleteImage(deviceGuid: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (imageId: string) => imageService.remove(deviceGuid, imageId),
        onSuccess: () => invalidate(queryClient, deviceGuid),
    });
}

export function useSetTitleImage(deviceGuid: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (imageId: string) => imageService.setTitle(deviceGuid, imageId),
        onSuccess: () => invalidate(queryClient, deviceGuid),
    });
}

function invalidate(queryClient: ReturnType<typeof useQueryClient>, deviceGuid: string): void {
    void queryClient.invalidateQueries({ queryKey: vehicleImagesQueryKey(deviceGuid) });
    void queryClient.invalidateQueries({ queryKey: vehiclesQueryKey });
}
