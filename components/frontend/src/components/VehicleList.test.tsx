import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren, ReactElement } from 'react';
import { VehicleList } from './VehicleList';
import { useDeviceStatus } from '../hooks/useDeviceStatus';
import { useRunProgress } from '../hooks/useRunProgress';
import type { VehicleResponse } from '../models/api';
import type { DeviceStatusUpdate } from '../models/realtime';

vi.mock('../hooks/useDeviceStatus', () => ({
    useDeviceStatus: vi.fn(),
}));
// Keep the row's progress bar off the real telemetry connection.
vi.mock('../hooks/useRunProgress', () => ({
    useRunProgress: vi.fn().mockReturnValue(null),
}));

const useDeviceStatusMock = vi.mocked(useDeviceStatus);
const useRunProgressMock = vi.mocked(useRunProgress);

// RunControls' command mutations need a query client.
function Providers({ children }: PropsWithChildren) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

function render(ui: ReactElement) {
    return rtlRender(ui, { wrapper: Providers });
}

const pendingVehicle: VehicleResponse = {
    deviceGuid: 'GUID-PENDING',
    name: 'pending device',
    owner: '',
    registrationStatus: 'pending',
    createdAt: '2026-06-07T10:00:00Z',
    metadata: {},
};

const registeredVehicle: VehicleResponse = {
    deviceGuid: 'GUID-REG',
    name: 'kart-1',
    owner: 'admin',
    registrationStatus: 'registered',
    createdAt: '2026-06-07T10:00:00Z',
    metadata: {},
};

const liveStatus: DeviceStatusUpdate = {
    deviceGuid: 'GUID-REG',
    state: 'Acquiring',
    uptimeMs: 60_000,
    batteryMv: 3987,
    batteryPct: 76,
    errorCode: 0,
    observedAtUtc: new Date().toISOString(),
};

describe('VehicleList', () => {
    it('marks pending vehicles and shows the no-signal placeholder without live status', () => {
        useDeviceStatusMock.mockReturnValue(null);

        render(<VehicleList vehicles={[pendingVehicle]} />);

        expect(screen.getByText('pending')).toBeInTheDocument();
        expect(screen.getByText('No signal')).toBeInTheDocument();
    });

    it('renders live state, battery and uptime when status arrives', () => {
        useDeviceStatusMock.mockReturnValue(liveStatus);

        render(<VehicleList vehicles={[registeredVehicle]} />);

        expect(screen.getByText('Acquiring')).toBeInTheDocument();
        expect(screen.getByText('3987 mV · 76 %')).toBeInTheDocument();
        expect(screen.getByText('1m 00s')).toBeInTheDocument();
        expect(screen.getByText('registered')).toBeInTheDocument();
    });

    it('shows the error indicator only when error bits are set', () => {
        useDeviceStatusMock.mockReturnValue({ ...liveStatus, errorCode: 4 });

        render(<VehicleList vehicles={[registeredVehicle]} />);

        expect(screen.getByTitle('Device reports error codes')).toBeInTheDocument();
    });

    it('offers the claim action on pending rows only', () => {
        useDeviceStatusMock.mockReturnValue(null);

        render(<VehicleList vehicles={[pendingVehicle, registeredVehicle]} />);

        expect(screen.getAllByRole('button', { name: 'Claim' })).toHaveLength(1);
    });

    it('shows live run progress while the device is acquiring (7.4)', () => {
        useDeviceStatusMock.mockReturnValue(liveStatus);
        useRunProgressMock.mockReturnValueOnce({
            deviceGuid: 'GUID-REG',
            sampledCount: 500,
            totalSamples: 1000,
            observedAtUtc: new Date().toISOString(),
        });

        render(<VehicleList vehicles={[registeredVehicle]} />);

        expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '50');
        expect(screen.getByText('500 / 1000 samples (50 %)')).toBeInTheDocument();
    });
});
