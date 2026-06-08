import { describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { HubConnectionState } from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import { useRunProgress } from './useRunProgress';
import { TelemetryConnection } from '../utils/signalrClient';
import type { RunProgressUpdate } from '../models/realtime';

function progress(
    deviceGuid: string,
    sampledCount: number,
    observedAtUtc: string,
): RunProgressUpdate {
    return { deviceGuid, sampledCount, totalSamples: 1000, observedAtUtc };
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
        emit: (update: RunProgressUpdate) => act(() => handlers.get('RunProgress')?.(update)),
    };
}

describe('useRunProgress', () => {
    it('surfaces progress for the subscribed device', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        const { result } = renderHook(() => useRunProgress('GUID-A', client));
        await waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', 'GUID-A']));

        hub.emit(progress('GUID-A', 100, '2026-06-07T12:00:00Z'));

        await waitFor(() => expect(result.current?.sampledCount).toBe(100));
    });

    it('ignores other devices and stale events', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        const { result } = renderHook(() => useRunProgress('GUID-A', client));
        await waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', 'GUID-A']));

        hub.emit(progress('GUID-B', 999, '2026-06-07T12:00:05Z'));
        hub.emit(progress('GUID-A', 500, '2026-06-07T12:00:05Z'));
        hub.emit(progress('GUID-A', 100, '2026-06-07T12:00:01Z')); // stale

        await waitFor(() => expect(result.current?.sampledCount).toBe(500));
    });

    it('releases the group subscription on unmount', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        const { unmount } = renderHook(() => useRunProgress('GUID-A', client));
        await waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', 'GUID-A']));

        unmount();

        await waitFor(() => expect(hub.invocations).toContainEqual(['Unsubscribe', 'GUID-A']));
    });
});
