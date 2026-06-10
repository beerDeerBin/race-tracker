import { describe, expect, it, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren, ReactElement } from 'react';
import { VehicleGallery } from './VehicleGallery';
import { imageService } from '../services/imageService';
import type { VehicleImageResponse, VehicleResponse } from '../models/api';

vi.mock('../services/imageService', async (importOriginal) => {
    const original = await importOriginal<typeof import('../services/imageService')>();
    return {
        ...original,
        imageService: {
            list: vi.fn(),
            upload: vi.fn(),
            remove: vi.fn(),
            setTitle: vi.fn(),
            getBlob: vi
                .fn()
                .mockResolvedValue(new Blob([new Uint8Array([1])], { type: 'image/png' })),
        },
    };
});

const service = vi.mocked(imageService);

function render_(ui: ReactElement) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    function Providers({ children }: PropsWithChildren) {
        return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
    }
    return render(ui, { wrapper: Providers });
}

const vehicle: VehicleResponse = {
    deviceGuid: 'GUID-Aa',
    name: 'kart-1',
    owner: 'admin',
    registrationStatus: 'registered',
    createdAt: '2026-06-07T10:00:00Z',
    metadata: {},
    titleImageId: 'img-1',
};

const images: VehicleImageResponse[] = [
    {
        id: 'img-1',
        fileName: 'one.png',
        contentType: 'image/png',
        length: 8,
        uploadedAt: '2026-06-07T10:00:00Z',
    },
    {
        id: 'img-2',
        fileName: 'two.png',
        contentType: 'image/png',
        length: 8,
        uploadedAt: '2026-06-07T10:01:00Z',
    },
];

describe('VehicleGallery', () => {
    beforeEach(() => {
        service.list.mockReset().mockResolvedValue(images);
        service.upload.mockReset().mockResolvedValue(images[0]!);
        service.remove.mockReset().mockResolvedValue(undefined);
        service.setTitle.mockReset().mockResolvedValue(undefined);
    });

    it('shows the empty state when there are no images', async () => {
        service.list.mockResolvedValueOnce([]);
        render_(<VehicleGallery vehicle={vehicle} />);

        expect(
            await screen.findByText("No images yet — upload one to set the vehicle's picture."),
        ).toBeInTheDocument();
    });

    it('marks the current title image and sets another as title', async () => {
        render_(<VehicleGallery vehicle={vehicle} />);

        // Only the title image (img-1) shows the badge.
        expect(await screen.findByText('Title')).toBeInTheDocument();

        await userEvent.click(screen.getByRole('button', { name: 'Set two.png as title image' }));

        expect(service.setTitle).toHaveBeenCalledWith('GUID-Aa', 'img-2');
    });

    it('deletes an image only after confirmation', async () => {
        render_(<VehicleGallery vehicle={vehicle} />);

        await userEvent.click(await screen.findByRole('button', { name: 'Delete one.png' }));
        // The confirmation dialog appears; nothing is deleted until confirmed.
        expect(service.remove).not.toHaveBeenCalled();

        await userEvent.click(screen.getByRole('button', { name: 'Delete' }));

        expect(service.remove).toHaveBeenCalledWith('GUID-Aa', 'img-1');
    });

    it('uploads a valid image', async () => {
        const { container } = render_(<VehicleGallery vehicle={vehicle} />);
        await screen.findByText('Title');

        const input = container.querySelector('input[type="file"]') as HTMLInputElement;
        const file = new File([new Uint8Array([1, 2, 3])], 'new.png', { type: 'image/png' });
        await userEvent.upload(input, file);

        await waitFor(() => expect(service.upload).toHaveBeenCalledWith('GUID-Aa', file));
    });

    it('uploads several files at once', async () => {
        const { container } = render_(<VehicleGallery vehicle={vehicle} />);
        await screen.findByText('Title');

        const input = container.querySelector('input[type="file"]') as HTMLInputElement;
        const a = new File([new Uint8Array([1])], 'a.png', { type: 'image/png' });
        const b = new File([new Uint8Array([2])], 'b.png', { type: 'image/png' });
        await userEvent.upload(input, [a, b]);

        await waitFor(() => {
            expect(service.upload).toHaveBeenCalledWith('GUID-Aa', a);
            expect(service.upload).toHaveBeenCalledWith('GUID-Aa', b);
        });
    });

    it('rejects an unsupported file type without uploading', async () => {
        const { container } = render_(<VehicleGallery vehicle={vehicle} />);
        await screen.findByText('Title');

        const input = container.querySelector('input[type="file"]') as HTMLInputElement;
        const file = new File(['x'], 'notes.txt', { type: 'text/plain' });
        // fireEvent bypasses the input's `accept` filter so the component's own guard is exercised.
        fireEvent.change(input, { target: { files: [file] } });

        expect(service.upload).not.toHaveBeenCalled();
        const alert = await screen.findByRole('alert');
        expect(within(alert).getByText(/Unsupported file type/)).toBeInTheDocument();
    });
});
