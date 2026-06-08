import { useEffect, useState } from 'react';
import { telemetryConnection } from '../utils/signalrClient';
import type { TelemetryConnection } from '../utils/signalrClient';
import type { DeviceStatusUpdate } from '../models/realtime';

/**
 * Live status of one device (/F60/, /F62/): joins the per-guid group on mount and leaves it
 * on unmount (the /F63/ "unsubscribe on navigation"). Updates merge newest-wins by
 * `observedAtUtc`, so a stale or retained event never overwrites a fresher one.
 */
export function useDeviceStatus(
    deviceGuid: string,
    connection: TelemetryConnection = telemetryConnection,
): DeviceStatusUpdate | null {
    const [status, setStatus] = useState<DeviceStatusUpdate | null>(null);

    useEffect(() => {
        let active = true;

        const offStatus = connection.onDeviceStatus((update) => {
            if (!active || update.deviceGuid !== deviceGuid) {
                return;
            }
            setStatus((current) =>
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
            offStatus();
            void releasePromise.then((release) => release());
        };
    }, [deviceGuid, connection]);

    return status;
}
