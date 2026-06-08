import { describe, expect, it } from 'vitest';
import { commandDisabledReasonKey, isCommandAllowed } from './commandValidity';
import type { DeviceCommand } from './commandValidity';
import type { DeviceState } from '../models/realtime';

describe('isCommandAllowed (/F34/ matrix, PROTOCOL §3/§4)', () => {
    const matrix: Array<[DeviceCommand, DeviceState | null, boolean]> = [
        ['connect', 'Idle', true],
        ['connect', 'Connected', false],
        ['connect', 'Acquiring', false],
        ['connect', null, false],
        ['startRun', 'Idle', false],
        ['startRun', 'Connected', true],
        ['startRun', 'Acquiring', false],
        ['startRun', null, false],
        ['disconnect', 'Idle', false],
        ['disconnect', 'Connected', true],
        ['disconnect', 'Acquiring', false],
        ['disconnect', null, false],
        ['reset', 'Idle', true],
        ['reset', 'Connected', true],
        ['reset', 'Acquiring', false],
        ['reset', null, false],
    ];

    it.each(matrix)('%s in state %s → %s', (command, state, expected) => {
        expect(isCommandAllowed(command, state)).toBe(expected);
    });
});

describe('commandDisabledReasonKey', () => {
    it('returns null for allowed commands', () => {
        expect(commandDisabledReasonKey('connect', 'Idle')).toBeNull();
        expect(commandDisabledReasonKey('reset', 'Connected')).toBeNull();
    });

    it('explains the missing live status', () => {
        expect(commandDisabledReasonKey('connect', null)).toBe('commands.reason.noSignal');
    });

    it('explains the running acquisition', () => {
        expect(commandDisabledReasonKey('reset', 'Acquiring')).toBe('commands.reason.acquiring');
    });

    it('explains the required state', () => {
        expect(commandDisabledReasonKey('connect', 'Connected')).toBe('commands.reason.needsIdle');
        expect(commandDisabledReasonKey('startRun', 'Idle')).toBe('commands.reason.needsConnected');
        expect(commandDisabledReasonKey('disconnect', 'Idle')).toBe(
            'commands.reason.needsConnected',
        );
    });
});
