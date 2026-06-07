import { describe, expect, it, vi } from 'vitest';
import { HubConnectionState } from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import { TelemetryConnection } from './signalrClient';
import type { DeviceStatusUpdate } from '../models/realtime';

function status(deviceGuid: string, observedAtUtc: string): DeviceStatusUpdate {
    return {
        deviceGuid,
        state: 'Connected',
        uptimeMs: 1000,
        batteryMv: 3987,
        batteryPct: 76,
        errorCode: 0,
        observedAtUtc,
    };
}

function fakeHub() {
    const handlers = new Map<string, (arg: unknown) => void>();
    let reconnected: (() => void) | null = null;
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
        onreconnected: vi.fn((callback: () => void) => {
            reconnected = callback;
        }),
    };

    return {
        connection: connection as unknown as HubConnection,
        raw: connection,
        invocations,
        emit: (event: string, update: DeviceStatusUpdate) => handlers.get(event)?.(update),
        emitRaw: (event: string, update: unknown) => handlers.get(event)?.(update),
        triggerReconnected: () => reconnected?.(),
    };
}

describe('TelemetryConnection', () => {
    it('starts lazily and joins the group on first subscribe only (ref-counted)', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        await client.subscribe('GUID-A');
        await client.subscribe('GUID-A');

        expect(hub.raw.start).toHaveBeenCalledTimes(1);
        expect(
            hub.invocations.filter(([m, g]) => m === 'Subscribe' && g === 'GUID-A'),
        ).toHaveLength(1);
    });

    it('joins the group exactly once for concurrent same-guid subscribes (StrictMode pattern)', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        // Regression: deciding the join after awaiting the start saw count 2 in both callers,
        // so nobody invoked Subscribe and live status silently never arrived.
        await Promise.all([client.subscribe('GUID-A'), client.subscribe('GUID-A')]);

        expect(
            hub.invocations.filter(([m, g]) => m === 'Subscribe' && g === 'GUID-A'),
        ).toHaveLength(1);
    });

    it('leaves the group and stops the connection when the last subscriber releases', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        const releaseFirst = await client.subscribe('GUID-A');
        const releaseSecond = await client.subscribe('GUID-A');

        await releaseFirst();
        expect(hub.invocations.some(([m]) => m === 'Unsubscribe')).toBe(false);
        expect(hub.raw.stop).not.toHaveBeenCalled();

        await releaseSecond();
        expect(hub.invocations).toContainEqual(['Unsubscribe', 'GUID-A']);
        expect(hub.raw.stop).toHaveBeenCalledTimes(1);
    });

    it('release is idempotent', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        const release = await client.subscribe('GUID-A');
        await release();
        await release();

        expect(hub.invocations.filter(([m]) => m === 'Unsubscribe')).toHaveLength(1);
    });

    it('replays every registered group after a reconnect', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        await client.subscribe('GUID-A');
        await client.subscribe('GUID-B');
        hub.invocations.length = 0;

        hub.triggerReconnected();
        await Promise.resolve();

        expect(hub.invocations).toContainEqual(['Subscribe', 'GUID-A']);
        expect(hub.invocations).toContainEqual(['Subscribe', 'GUID-B']);
    });

    it('dispatches DeviceStatus events to registered handlers until deregistered', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);
        const received: DeviceStatusUpdate[] = [];
        const off = client.onDeviceStatus((update) => received.push(update));

        await client.subscribe('GUID-A');
        hub.emit('DeviceStatus', status('GUID-A', '2026-06-07T12:00:00Z'));
        off();
        hub.emit('DeviceStatus', status('GUID-A', '2026-06-07T12:00:01Z'));

        expect(received).toHaveLength(1);
        expect(received[0]!.deviceGuid).toBe('GUID-A');
    });

    it('dispatches RunProgress events to registered handlers (7.4)', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);
        const received: unknown[] = [];
        const off = client.onRunProgress((update) => received.push(update));

        await client.subscribe('GUID-A');
        const update = {
            deviceGuid: 'GUID-A',
            sampledCount: 100,
            totalSamples: 1000,
            observedAtUtc: '2026-06-07T12:00:00Z',
        };
        hub.emitRaw('RunProgress', update);
        off();
        hub.emitRaw('RunProgress', update);

        expect(received).toHaveLength(1);
        expect(received[0]).toEqual(update);
    });

    it('keeps the case-sensitive guid verbatim in hub invocations', async () => {
        const hub = fakeHub();
        const client = new TelemetryConnection(() => hub.connection);

        await client.subscribe('AABB-CCdd-0099');

        expect(hub.invocations).toContainEqual(['Subscribe', 'AABB-CCdd-0099']);
    });
});
