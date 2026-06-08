import type { DeviceState } from '../models/realtime';

/**
 * The /F34/ command-validity matrix (PROTOCOL §3/§4), single source of truth for the UI:
 * CONNECT only in Idle · START_RUN only in Connected · DISCONNECT only in Connected ·
 * RESET in Idle or Connected · nothing while Acquiring or without a live status.
 */

export type DeviceCommand = 'connect' | 'startRun' | 'disconnect' | 'reset';

const ALLOWED: Record<DeviceCommand, readonly DeviceState[]> = {
    connect: ['Idle'],
    startRun: ['Connected'],
    disconnect: ['Connected'],
    reset: ['Idle', 'Connected'],
};

export function isCommandAllowed(command: DeviceCommand, state: DeviceState | null): boolean {
    return state !== null && ALLOWED[command].includes(state);
}

/** i18n key explaining why a command is disabled; null when it is allowed. */
export function commandDisabledReasonKey(
    command: DeviceCommand,
    state: DeviceState | null,
): string | null {
    if (isCommandAllowed(command, state)) {
        return null;
    }
    if (state === null) {
        return 'commands.reason.noSignal';
    }
    if (state === 'Acquiring') {
        return 'commands.reason.acquiring';
    }
    return command === 'connect' ? 'commands.reason.needsIdle' : 'commands.reason.needsConnected';
}
