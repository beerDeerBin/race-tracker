import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { HubConnectionState } from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import type { PropsWithChildren } from 'react';
import { useLiveRun } from './useLiveRun';
import { samplesQueryKey } from './useSamples';
import { TelemetryConnection } from '../utils/signalrClient';
import { sampleService } from '../services/sampleService';
import type { Sample } from '../models/graphql';
import type { RunProgressUpdate } from '../models/realtime';

vi.mock('../services/sampleService', () => ({
    PAGE_SIZE: 50_000,
    sampleService: { getSamples: vi.fn(), getAllSamples: vi.fn() },
}));

const getSamplesMock = vi.mocked(sampleService.getSamples);

const RUN_ID = 'run-1';
const DEVICE = 'GUID-A';

function sample(index: number): Sample {
    return { index, ax: 0, ay: 0, az: 9.81, gx: 0, gy: 0, gz: 0 };
}

function progress(deviceGuid: string): RunProgressUpdate {
    return {
        deviceGuid,
        sampledCount: 0,
        totalSamples: 1000,
        observedAtUtc: '2026-06-07T12:00:00Z',
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
        emitProgress: (update: RunProgressUpdate) => handlers.get('RunProgress')?.(update),
    };
}

function setup(initialCache?: Sample[]) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    if (initialCache) {
        client.setQueryData(samplesQueryKey(RUN_ID), initialCache);
    }
    const hub = fakeHub();
    const connection = new TelemetryConnection(() => hub.connection);
    const wrapper = ({ children }: PropsWithChildren) => (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    return { client, hub, connection, wrapper };
}

describe('useLiveRun', () => {
    beforeEach(() => {
        vi.useFakeTimers();
        getSamplesMock.mockReset();
    });
    afterEach(() => {
        vi.runOnlyPendingTimers();
        vi.useRealTimers();
    });

    it('tail-fetches from the cached last index + 1 and appends new rows', async () => {
        const { client, hub, connection, wrapper } = setup([sample(0), sample(1)]);
        getSamplesMock.mockResolvedValueOnce([sample(2), sample(3)]);

        renderHook(() => useLiveRun(DEVICE, RUN_ID, connection), { wrapper });
        await vi.waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', DEVICE]));

        hub.emitProgress(progress(DEVICE));

        await vi.waitFor(() => expect(getSamplesMock).toHaveBeenCalledWith(RUN_ID, 2, 50_000));
        await vi.waitFor(() =>
            expect(client.getQueryData<Sample[]>(samplesQueryKey(RUN_ID))).toHaveLength(4),
        );
    });

    it('uses fromIndex 0 when the cache is empty', async () => {
        const { hub, connection, wrapper } = setup();
        getSamplesMock.mockResolvedValue([]);

        renderHook(() => useLiveRun(DEVICE, RUN_ID, connection), { wrapper });
        await vi.waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', DEVICE]));

        hub.emitProgress(progress(DEVICE));

        await vi.waitFor(() => expect(getSamplesMock).toHaveBeenCalledWith(RUN_ID, 0, 50_000));
    });

    it('drops overlapping rows when appending (dedupe by index)', async () => {
        const { client, hub, connection, wrapper } = setup([sample(0), sample(1)]);
        getSamplesMock.mockResolvedValueOnce([sample(1), sample(2)]); // index 1 overlaps

        renderHook(() => useLiveRun(DEVICE, RUN_ID, connection), { wrapper });
        await vi.waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', DEVICE]));

        hub.emitProgress(progress(DEVICE));

        await vi.waitFor(() =>
            expect(
                client.getQueryData<Sample[]>(samplesQueryKey(RUN_ID))!.map((s) => s.index),
            ).toEqual([0, 1, 2]),
        );
    });

    it('throttles rapid events to one immediate fetch, then a trailing catch-up', async () => {
        const { hub, connection, wrapper } = setup([sample(0)]);
        getSamplesMock.mockResolvedValue([]);

        renderHook(() => useLiveRun(DEVICE, RUN_ID, connection), { wrapper });
        await vi.waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', DEVICE]));

        hub.emitProgress(progress(DEVICE));
        hub.emitProgress(progress(DEVICE));
        hub.emitProgress(progress(DEVICE));
        await vi.waitFor(() => expect(getSamplesMock).toHaveBeenCalledTimes(1));

        // Trailing fetch fires after the quiet window.
        await vi.advanceTimersByTimeAsync(1500);
        expect(getSamplesMock).toHaveBeenCalledTimes(2);
    });

    it('coalesces a trigger that lands during a slow fetch into a re-run (end-burst safety)', async () => {
        const { hub, connection, wrapper } = setup([sample(0)]);
        // First fetch hangs until we release it; the trailing trigger lands meanwhile.
        let releaseFirst!: (rows: Sample[]) => void;
        const firstFetch = new Promise<Sample[]>((resolve) => {
            releaseFirst = resolve;
        });
        getSamplesMock.mockReturnValueOnce(firstFetch).mockResolvedValue([]);

        renderHook(() => useLiveRun(DEVICE, RUN_ID, connection), { wrapper });
        await vi.waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', DEVICE]));

        hub.emitProgress(progress(DEVICE)); // leading fetch starts, hangs
        await vi.waitFor(() => expect(getSamplesMock).toHaveBeenCalledTimes(1));

        await vi.advanceTimersByTimeAsync(1500); // trailing timer fires while fetch in flight
        expect(getSamplesMock).toHaveBeenCalledTimes(1); // swallowed by inFlight...

        releaseFirst([]);
        // ...but the coalesced pending flag re-fires once the slow fetch resolves.
        await vi.waitFor(() => expect(getSamplesMock).toHaveBeenCalledTimes(2));
    });

    it('ignores progress for other devices', async () => {
        const { hub, connection, wrapper } = setup([sample(0)]);
        getSamplesMock.mockResolvedValue([]);

        renderHook(() => useLiveRun(DEVICE, RUN_ID, connection), { wrapper });
        await vi.waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', DEVICE]));

        hub.emitProgress(progress('OTHER-DEVICE'));
        await vi.advanceTimersByTimeAsync(2000);

        expect(getSamplesMock).not.toHaveBeenCalled();
    });

    it('releases the group subscription on unmount', async () => {
        const { hub, connection, wrapper } = setup([sample(0)]);

        const { unmount } = renderHook(() => useLiveRun(DEVICE, RUN_ID, connection), { wrapper });
        await vi.waitFor(() => expect(hub.invocations).toContainEqual(['Subscribe', DEVICE]));

        unmount();

        await vi.waitFor(() => expect(hub.invocations).toContainEqual(['Unsubscribe', DEVICE]));
    });
});
