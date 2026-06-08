import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { telemetryConnection } from '../utils/signalrClient';
import type { TelemetryConnection } from '../utils/signalrClient';
import { PAGE_SIZE, sampleService } from '../services/sampleService';
import { samplesQueryKey } from './useSamples';
import { logger } from '../utils/logger';
import type { Sample } from '../models/graphql';

/**
 * Live measurement view (/F64/): grows the viewed run's chart as new batches arrive,
 * without a reload and without polling. The 6.3 `RunProgress` push is only the trigger
 * (no second push path) — the sample data still comes from the GraphQL read path. New
 * rows are appended into the existing `useSamples` cache, so the run detail page
 * re-renders from cache.
 *
 * RunProgress carries no runId, so a progress signal for this device tail-fetches the
 * viewed run: matching data returns new rows, a different (newer) run returns nothing.
 */

const THROTTLE_MS = 500;
const TRAILING_MS = 1500;

export function useLiveRun(
    deviceGuid: string,
    runId: string,
    connection: TelemetryConnection = telemetryConnection,
): void {
    const queryClient = useQueryClient();

    useEffect(() => {
        let active = true;
        let inFlight = false;
        // A trigger arriving while a fetch is in flight sets this so the in-flight fetch
        // re-runs once on completion. Without it the end-of-run burst (where the trailing
        // timer often fires during the leading fetch) would be lost permanently, since no
        // further progress events arrive to re-arm the catch-up.
        let pending = false;
        let lastFetchAt = 0;
        let trailingTimer: ReturnType<typeof setTimeout> | undefined;

        const tailFetch = async () => {
            if (!active) {
                return;
            }
            if (inFlight) {
                pending = true;
                return;
            }
            inFlight = true;
            try {
                // NOTE: if the initial getAllSamples (useSamples) is still in flight, this
                // reads an empty cache and fetches from 0; React Query may then overwrite
                // with the initial result. Self-heals on the next trigger / trailing fetch
                // (acceptable for an S-priority live view).
                const cached = queryClient.getQueryData<Sample[]>(samplesQueryKey(runId)) ?? [];
                const fromIndex = cached.length > 0 ? cached[cached.length - 1]!.index + 1 : 0;

                const page = await sampleService.getSamples(runId, fromIndex, PAGE_SIZE);
                if (active && page.length > 0) {
                    // Append only genuinely-new indices (guard against overlap with the cache).
                    queryClient.setQueryData<Sample[]>(samplesQueryKey(runId), (current) => {
                        const base = current ?? [];
                        const lastIndex = base.length > 0 ? base[base.length - 1]!.index : -1;
                        const fresh = page.filter((sample) => sample.index > lastIndex);
                        return fresh.length > 0 ? [...base, ...fresh] : base;
                    });
                }
            } catch (error) {
                logger.warn('Live tail fetch failed', { runId, error });
            } finally {
                inFlight = false;
                // A trigger coalesced during this fetch → run once more to catch late data.
                if (active && pending) {
                    pending = false;
                    void tailFetch();
                }
            }
        };

        const onProgress = () => {
            const now = Date.now();
            // Leading-edge throttle: fetch immediately at most every THROTTLE_MS.
            if (now - lastFetchAt >= THROTTLE_MS) {
                lastFetchAt = now;
                void tailFetch();
            }
            // Trailing fetch: persistence lags the event, so re-arm a catch-up after quiet.
            clearTimeout(trailingTimer);
            trailingTimer = setTimeout(() => {
                lastFetchAt = Date.now();
                void tailFetch();
            }, TRAILING_MS);
        };

        const offProgress = connection.onRunProgress((update) => {
            if (update.deviceGuid === deviceGuid) {
                onProgress();
            }
        });
        const releasePromise = connection.subscribe(deviceGuid);

        return () => {
            active = false;
            offProgress();
            clearTimeout(trailingTimer);
            void releasePromise.then((release) => release());
        };
    }, [deviceGuid, runId, connection, queryClient]);
}
