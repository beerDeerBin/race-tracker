import { useEffect, useState } from 'react';
import { telemetryConnection } from '../utils/signalrClient';
import type { TelemetryConnection } from '../utils/signalrClient';
import type { RunProgressUpdate } from '../models/realtime';

/**
 * Live run progress of one device (/F61/): same per-guid group subscription as
 * useDeviceStatus (ref-counted, shared), newest-wins by `observedAtUtc`.
 */
export function useRunProgress(
    deviceGuid: string,
    connection: TelemetryConnection = telemetryConnection,
): RunProgressUpdate | null {
    const [progress, setProgress] = useState<RunProgressUpdate | null>(null);

    useEffect(() => {
        let active = true;

        const offProgress = connection.onRunProgress((update) => {
            if (!active || update.deviceGuid !== deviceGuid) {
                return;
            }
            setProgress((current) =>
                current === null ||
                new Date(update.observedAtUtc).getTime() >=
                    new Date(current.observedAtUtc).getTime()
                    ? update
                    : current,
            );
        });

        const releasePromise = connection.subscribe(deviceGuid);

        return () => {
            active = false;
            offProgress();
            void releasePromise.then((release) => release());
        };
    }, [deviceGuid, connection]);

    return progress;
}
