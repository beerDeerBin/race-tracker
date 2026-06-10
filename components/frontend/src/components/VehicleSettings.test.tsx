import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren, ReactElement } from 'react';
import { VehicleSettings } from './VehicleSettings';
import { vehicleService } from '../services/vehicleService';
import type { VehicleResponse } from '../models/api';

vi.mock('../services/vehicleService', async (importOriginal) => {
    const original = await importOriginal<typeof import('../services/vehicleService')>();
    return {
        ...original,
        vehicleService: { update: vi.fn(), unclaim: vi.fn(), list: vi.fn(), claim: vi.fn() },
    };
});

const service = vi.mocked(vehicleService);

function render_(ui: ReactElement) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    function Providers({ children }: PropsWithChildren) {
        return (
            <MemoryRouter>
                <QueryClientProvider client={client}>{children}</QueryClientProvider>
            </MemoryRouter>
        );
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
    titleImageId: null,
};

describe('VehicleSettings', () => {
    beforeEach(() => {
        service.update.mockReset().mockResolvedValue({ ...vehicle, name: 'renamed' });
        service.unclaim
            .mockReset()
            .mockResolvedValue({
                ...vehicle,
                name: vehicle.deviceGuid,
                owner: '',
                registrationStatus: 'pending',
            });
    });

    it('renames the vehicle', async () => {
        render_(<VehicleSettings vehicle={vehicle} />);

        const input = screen.getByLabelText('Vehicle name');
        await userEvent.clear(input);
        await userEvent.type(input, 'renamed');
        await userEvent.click(screen.getByRole('button', { name: 'Save' }));

        await waitFor(() =>
            expect(service.update).toHaveBeenCalledWith('GUID-Aa', { name: 'renamed' }),
        );
    });

    it('releases the vehicle only after confirmation', async () => {
        render_(<VehicleSettings vehicle={vehicle} />);

        await userEvent.click(screen.getByRole('button', { name: 'Release vehicle' }));
        expect(service.unclaim).not.toHaveBeenCalled();

        // Confirm inside the dialog (disambiguate from the danger-zone trigger of the same name).
        const dialog = screen.getByRole('dialog');
        await userEvent.click(within(dialog).getByRole('button', { name: 'Release vehicle' }));

        await waitFor(() => expect(service.unclaim).toHaveBeenCalledWith('GUID-Aa'));
    });
});
