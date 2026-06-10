import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren, ReactElement } from 'react';
import { ClaimDialog } from './ClaimDialog';
import { AuthContext } from '../context/AuthContext';
import { DeviceNotFoundError, vehicleService } from '../services/vehicleService';
import { imageService } from '../services/imageService';
import type { VehicleResponse } from '../models/api';

vi.mock('../services/vehicleService', async (importOriginal) => {
    const original = await importOriginal<typeof import('../services/vehicleService')>();
    return {
        DeviceNotFoundError: original.DeviceNotFoundError,
        vehicleService: { list: vi.fn(), claim: vi.fn() },
    };
});

vi.mock('../services/imageService', async (importOriginal) => {
    const original = await importOriginal<typeof import('../services/imageService')>();
    return {
        ...original,
        imageService: {
            upload: vi.fn(),
            list: vi.fn(),
            remove: vi.fn(),
            setTitle: vi.fn(),
            getBlob: vi.fn(),
        },
    };
});

const claimMock = vi.mocked(vehicleService.claim);
const uploadMock = vi.mocked(imageService.upload);

const pending: VehicleResponse = {
    deviceGuid: 'GUID-PENDING',
    name: 'GUID-PENDING',
    owner: '',
    registrationStatus: 'pending',
    createdAt: '2026-06-07T10:00:00Z',
    metadata: {},
};

function renderDialog(ui: ReactElement) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const auth = {
        user: 'admin',
        role: 'admin',
        isAuthenticated: true,
        login: vi.fn(),
        logout: vi.fn(),
    };
    function Providers({ children }: PropsWithChildren) {
        return (
            <QueryClientProvider client={client}>
                <AuthContext.Provider value={auth}>{children}</AuthContext.Provider>
            </QueryClientProvider>
        );
    }
    return render(ui, { wrapper: Providers });
}

describe('ClaimDialog', () => {
    beforeEach(() => {
        claimMock.mockReset();
        uploadMock.mockReset();
    });

    it('submits the trimmed name (owner omitted when blank) and closes on success', async () => {
        claimMock.mockResolvedValueOnce({ ...pending, registrationStatus: 'registered' });
        const onClose = vi.fn();
        renderDialog(<ClaimDialog vehicle={pending} onClose={onClose} />);

        await userEvent.type(screen.getByLabelText('Vehicle name'), '  kart-1  ');
        await userEvent.click(screen.getByRole('button', { name: 'Claim' }));

        await waitFor(() => expect(onClose).toHaveBeenCalled());
        expect(claimMock).toHaveBeenCalledWith('GUID-PENDING', { name: 'kart-1' });
    });

    it('passes a provided owner through', async () => {
        claimMock.mockResolvedValueOnce({ ...pending, registrationStatus: 'registered' });
        renderDialog(<ClaimDialog vehicle={pending} onClose={vi.fn()} />);

        await userEvent.type(screen.getByLabelText('Vehicle name'), 'kart-1');
        await userEvent.type(screen.getByLabelText('Owner'), 'alice');
        await userEvent.click(screen.getByRole('button', { name: 'Claim' }));

        await waitFor(() =>
            expect(claimMock).toHaveBeenCalledWith('GUID-PENDING', {
                name: 'kart-1',
                owner: 'alice',
            }),
        );
    });

    it('shows the not-found message when the device vanished', async () => {
        claimMock.mockRejectedValueOnce(new DeviceNotFoundError('GUID-PENDING'));
        const onClose = vi.fn();
        renderDialog(<ClaimDialog vehicle={pending} onClose={onClose} />);

        await userEvent.type(screen.getByLabelText('Vehicle name'), 'kart-1');
        await userEvent.click(screen.getByRole('button', { name: 'Claim' }));

        expect(await screen.findByRole('alert')).toHaveTextContent(
            'This device is no longer known.',
        );
        expect(onClose).not.toHaveBeenCalled();
    });

    it('never submits a whitespace-only name', async () => {
        renderDialog(<ClaimDialog vehicle={pending} onClose={vi.fn()} />);

        await userEvent.type(screen.getByLabelText('Vehicle name'), '   ');
        await userEvent.click(screen.getByRole('button', { name: 'Claim' }));

        expect(claimMock).not.toHaveBeenCalled();
    });

    it('closes on Escape without claiming', async () => {
        const onClose = vi.fn();
        renderDialog(<ClaimDialog vehicle={pending} onClose={onClose} />);

        await userEvent.keyboard('{Escape}');

        expect(onClose).toHaveBeenCalled();
        expect(claimMock).not.toHaveBeenCalled();
    });

    it('cancel closes without claiming', async () => {
        const onClose = vi.fn();
        renderDialog(<ClaimDialog vehicle={pending} onClose={onClose} />);

        await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));

        expect(onClose).toHaveBeenCalled();
        expect(claimMock).not.toHaveBeenCalled();
    });

    it('does not upload anything when no image was attached', async () => {
        claimMock.mockResolvedValueOnce({ ...pending, registrationStatus: 'registered' });
        renderDialog(<ClaimDialog vehicle={pending} onClose={vi.fn()} />);

        await userEvent.type(screen.getByLabelText('Vehicle name'), 'kart-1');
        await userEvent.click(screen.getByRole('button', { name: 'Claim' }));

        await waitFor(() => expect(claimMock).toHaveBeenCalled());
        expect(uploadMock).not.toHaveBeenCalled();
    });

    it('uploads the attached image after a successful claim, then closes', async () => {
        claimMock.mockResolvedValueOnce({ ...pending, registrationStatus: 'registered' });
        uploadMock.mockResolvedValueOnce({
            id: 'img-1',
            fileName: 'kart.png',
            contentType: 'image/png',
            length: 3,
            uploadedAt: '2026-06-07T10:00:00Z',
        });
        const onClose = vi.fn();
        const { container } = renderDialog(<ClaimDialog vehicle={pending} onClose={onClose} />);

        await userEvent.type(screen.getByLabelText('Vehicle name'), 'kart-1');
        const input = container.querySelector('input[type="file"]') as HTMLInputElement;
        const file = new File([new Uint8Array([1, 2, 3])], 'kart.png', { type: 'image/png' });
        await userEvent.upload(input, file);
        await userEvent.click(screen.getByRole('button', { name: 'Claim' }));

        await waitFor(() => expect(uploadMock).toHaveBeenCalledWith('GUID-PENDING', file));
        await waitFor(() => expect(onClose).toHaveBeenCalled());
    });
});
