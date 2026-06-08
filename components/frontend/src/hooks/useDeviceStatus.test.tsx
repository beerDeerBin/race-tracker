import { describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { HubConnectionState } from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import { useDeviceStatus } from './useDeviceStatus';
import { TelemetryConnection } from '../utils/signalrClient';
import type { DeviceStatusUpdate } from '../models/realtime';

function status(deviceGuid: string, observedAtUtc: string, uptimeMs = 1000): DeviceStatusUpdate {
    return {
        deviceGuid,
        state: 'Acquiring',
        uptimeMs,
        batteryMv: 3987,
        batteryPct: 76,
        errorCode: 0,
        observedAtUtc,
    };
}

function fakeHub() {
    const handlers = new Map<string, (arg: unknown) => void>();
    const invocations: Array<[string, string]> = [];

    const connection = {
        state: HubConnectionState.Disconnected,
        start: vi.fn(async () => {
            connection.state = HubConnectionState.Connected;
        }),
        stop: vi.fn(async () => {
            connection.state = HubConnectionState.Disconnected;
        }),
        invoke: vi.fn(async (method: string, deviceGuid: string) => {
            invocations.push([method, deviceGuid]);
        }),
        on: vi.fn((event: string, handler: (arg: unknown) => void) => {
            handlers.set(event, handler);
        }),
        onreconnected: vi.fn(),
    };

    return {
        connection: connection as unknown as HubConnection,
        invocations,
        emit: (update: DeviceStatusUpdate) => act(() => handlers.get('DeviceStatus')?.(update)),
    };
}

describe('useDeviceStatus', () => {
    it('subscribes to the guid group and surfaces its updates', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        const { result } = renderHook(() => useDeviceStatus('GUID-A', client));
        await waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', 'GUID-A']));

        hub.emit(status('GUID-A', '2026-06-07T12:00:00Z'));

        await waitFor(() => expect(result.current?.uptimeMs).toBe(1000));
    });

    it('ignores updates for other devices', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        const { result } = renderHook(() => useDeviceStatus('GUID-A', client));
        await waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', 'GUID-A']));

        hub.emit(status('GUID-B', '2026-06-07T12:00:00Z'));

        expect(result.current).toBeNull();
    });

    it('keeps the newest update by observedAtUtc (stale events never win)', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        const { result } = renderHook(() => useDeviceStatus('GUID-A', client));
        await waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', 'GUID-A']));

        hub.emit(status('GUID-A', '2026-06-07T12:00:05Z', 5000));
        hub.emit(status('GUID-A', '2026-06-07T12:00:01Z', 1000)); // stale

        await waitFor(() => expect(result.current?.uptimeMs).toBe(5000));
    });

    it('releases the group subscription on unmount', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        const { unmount } = renderHook(() => useDeviceStatus('GUID-A', client));
        await waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', 'GUID-A']));

        unmount();

        await waitFor(() => expect(hub.invocations).toContainEqual(['Unsubscribe', 'GUID-A']));
    });
});
