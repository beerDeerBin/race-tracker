import axios from 'axios';
import { httpClient } from '../utils/httpClient';
import { config } from '../utils/config';
import { encodeGuid } from '../utils/encodeGuid';
import type { VehicleImageResponse } from '../models/api';

/** Thrown when the targeted image (or its vehicle) is unknown (404) — keeps transport out of the UI. */
export class ImageNotFoundError extends Error {
    constructor(imageId: string) {
        super(`Image ${imageId} is not known`);
        this.name = 'ImageNotFoundError';
    }
}

/** Client-side mirror of the backend allowlist + size cap (Management:Images), for early feedback. */
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024;
export const ALLOWED_IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/webp', 'image/gif'] as const;

function imagesUrl(deviceGuid: string): string {
    return `${config.managementUrl}/vehicles/${encodeGuid(deviceGuid)}/images`;
}

/**
 * Vehicle gallery resource of the management service: the only place that knows the
 * `/vehicles/{guid}/images` routes. The device guid is the case-sensitive correlation key and is
 * only ever URL-encoded (never re-cased). Binaries are fetched as blobs through the shared auth'd
 * HTTP instance, so `<img>` tags can render them via object URLs without leaking the bearer token.
 */
export const imageService = {
    async list(deviceGuid: string): Promise<VehicleImageResponse[]> {
        const { data } = await httpClient.get<VehicleImageResponse[]>(imagesUrl(deviceGuid));
        return data;
    },

    async upload(deviceGuid: string, file: File): Promise<VehicleImageResponse> {
        const form = new FormData();
        form.append('file', file, file.name);
        const { data } = await httpClient.post<VehicleImageResponse>(imagesUrl(deviceGuid), form);
        return data;
    },

    async remove(deviceGuid: string, imageId: string): Promise<void> {
        await httpClient.delete(`${imagesUrl(deviceGuid)}/${encodeGuid(imageId)}`);
    },

    /** Sets the image as the vehicle's title image; maps a 404 to ImageNotFoundError. */
    async setTitle(deviceGuid: string, imageId: string): Promise<void> {
        try {
            await httpClient.put(`${imagesUrl(deviceGuid)}/${encodeGuid(imageId)}/title`);
        } catch (error) {
            if (axios.isAxiosError(error) && error.response?.status === 404) {
                throw new ImageNotFoundError(imageId);
            }
            throw error;
        }
    },

    /** Fetches the raw image bytes (authenticated) so the caller can build an object URL. */
    async getBlob(deviceGuid: string, imageId: string): Promise<Blob> {
        const { data } = await httpClient.get<Blob>(
            `${imagesUrl(deviceGuid)}/${encodeGuid(imageId)}`,
            { responseType: 'blob' },
        );
        return data;
    },
};
