import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren, ReactElement } from 'react';
import { VehicleAvatar } from './VehicleAvatar';
import { imageService } from '../services/imageService';
import type { VehicleResponse } from '../models/api';

vi.mock('../services/imageService', () => ({
    imageService: { getBlob: vi.fn() },
}));

const getBlobMock = vi.mocked(imageService.getBlob);

function render_(ui: ReactElement) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    function Providers({ children }: PropsWithChildren) {
        return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
    }
    return render(ui, { wrapper: Providers });
}

const base: VehicleResponse = {
    deviceGuid: 'GUID-Aa',
    name: 'kart-1',
    owner: 'admin',
    registrationStatus: 'registered',
    createdAt: '2026-06-07T10:00:00Z',
    metadata: {},
};

describe('VehicleAvatar', () => {
    beforeEach(() => getBlobMock.mockReset());

    it('renders a non-interactive placeholder when there is no title image', () => {
        render_(<VehicleAvatar vehicle={{ ...base, titleImageId: null }} />);

        expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });

    it('renders a button that opens a full-screen preview when a title image is set', async () => {
        getBlobMock.mockResolvedValue(new Blob([new Uint8Array([1])], { type: 'image/png' }));
        render_(<VehicleAvatar vehicle={{ ...base, titleImageId: 'img-1' }} />);

        await userEvent.click(screen.getByRole('button', { name: 'Open image of kart-1' }));

        expect(screen.getByRole('dialog')).toBeInTheDocument();
    });
});
