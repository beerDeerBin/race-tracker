import { useMutation } from '@tanstack/react-query';
import { commandService } from '../services/commandService';
import type { StartRunRequest } from '../models/api';

/**
 * Device command mutations (/F30/–/F33/). Commands are fire-and-forget (202): success
 * means "queued for delivery"; the resulting state change arrives via the live status.
 */
export function useCommands(deviceGuid: string) {
    const connect = useMutation({
        mutationFn: () => commandService.connect(deviceGuid),
    });
    const startRun = useMutation({
        mutationFn: (request: StartRunRequest) => commandService.startRun(deviceGuid, request),
    });
    const disconnect = useMutation({
        mutationFn: () => commandService.disconnect(deviceGuid),
    });
    const reset = useMutation({
        mutationFn: () => commandService.reset(deviceGuid),
    });

    return { connect, startRun, disconnect, reset };
}
