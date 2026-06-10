import { describe, expect, it, vi } from 'vitest';
import { AxiosError } from 'axios';
import { ImageNotFoundError, imageService } from './imageService';
import { httpClient } from '../utils/httpClient';
import { config } from '../utils/config';
import type { VehicleImageResponse } from '../models/api';

const image: VehicleImageResponse = {
    id: 'img-1',
    fileName: 'kart.png',
    contentType: 'image/png',
    length: 8,
    uploadedAt: '2026-06-07T10:00:00Z',
};

// A case-sensitive guid: it must only ever be URL-encoded, never re-cased.
const guid = 'GUID-Aa';
const base = `${config.managementUrl}/vehicles/${guid}/images`;

describe('imageService', () => {
    it('lists from the encoded, case-preserved images route', async () => {
        const get = vi.spyOn(httpClient, 'get').mockResolvedValueOnce({ data: [image] });

        const result = await imageService.list(guid);

        expect(get).toHaveBeenCalledWith(base);
        expect(result).toEqual([image]);
    });

    it('uploads the file as multipart form data', async () => {
        const post = vi.spyOn(httpClient, 'post').mockResolvedValueOnce({ data: image });
        const file = new File([new Uint8Array([1, 2, 3])], 'kart.png', { type: 'image/png' });

        const result = await imageService.upload(guid, file);

        expect(post).toHaveBeenCalledWith(base, expect.any(FormData));
        const form = post.mock.calls[0]![1] as FormData;
        // FormData.append clones the File when a filename is given — assert by name, not identity.
        const sent = form.get('file') as File;
        expect(sent.name).toBe('kart.png');
        expect(sent.type).toBe('image/png');
        expect(result).toEqual(image);
    });

    it('deletes by encoded image id', async () => {
        const del = vi.spyOn(httpClient, 'delete').mockResolvedValueOnce({ data: undefined });

        await imageService.remove(guid, 'img-1');

        expect(del).toHaveBeenCalledWith(`${base}/img-1`);
    });

    it('sets the title via the title route', async () => {
        const put = vi.spyOn(httpClient, 'put').mockResolvedValueOnce({ data: undefined });

        await imageService.setTitle(guid, 'img-1');

        expect(put).toHaveBeenCalledWith(`${base}/img-1/title`);
    });

    it('maps a 404 on set-title to ImageNotFoundError', async () => {
        const notFound = new AxiosError('Not Found');
        notFound.response = { status: 404 } as AxiosError['response'];
        vi.spyOn(httpClient, 'put').mockRejectedValueOnce(notFound);

        await expect(imageService.setTitle(guid, 'ghost')).rejects.toBeInstanceOf(
            ImageNotFoundError,
        );
    });

    it('fetches the binary as a blob', async () => {
        const blob = new Blob([new Uint8Array([1])], { type: 'image/png' });
        const get = vi.spyOn(httpClient, 'get').mockResolvedValueOnce({ data: blob });

        const result = await imageService.getBlob(guid, 'img-1');

        expect(get).toHaveBeenCalledWith(`${base}/img-1`, { responseType: 'blob' });
        expect(result).toBe(blob);
    });
});
