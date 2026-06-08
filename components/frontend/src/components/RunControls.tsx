import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Play, Power, RotateCcw, Unplug } from 'lucide-react';
import type { ComponentType } from 'react';
import { StartRunDialog } from './StartRunDialog';
import { useCommands } from '../hooks/useCommands';
import { commandDisabledReasonKey, isCommandAllowed } from '../utils/commandValidity';
import type { DeviceCommand } from '../utils/commandValidity';
import type { DeviceState } from '../models/realtime';

const ICONS: Record<DeviceCommand, ComponentType<{ className?: string }>> = {
    connect: Power,
    startRun: Play,
    disconnect: Unplug,
    reset: RotateCcw,
};

/**
 * Per-vehicle run control (/F30/–/F33/): the four commands as compact buttons, with
 * invalid actions disabled and explained per the /F34/ validity matrix. "Start run"
 * collects the parameters in a dialog.
 */
export function RunControls({
    deviceGuid,
    state,
}: {
    deviceGuid: string;
    state: DeviceState | null;
}) {
    const { t } = useTranslation();
    const commands = useCommands(deviceGuid);
    const [showStartDialog, setShowStartDialog] = useState(false);

    // A dispatch error shouldn't outlive the device state it happened in — once the state
    // moves on, the failed command may not even be retryable anymore (sticky otherwise).
    const { reset: resetConnect } = commands.connect;
    const { reset: resetDisconnect } = commands.disconnect;
    const { reset: resetReset } = commands.reset;
    useEffect(() => {
        resetConnect();
        resetDisconnect();
        resetReset();
    }, [state, resetConnect, resetDisconnect, resetReset]);

    const anyFailed =
        commands.connect.isError || commands.disconnect.isError || commands.reset.isError;

    const button = (
        command: DeviceCommand,
        labelKey: string,
        onClick: () => void,
        busy: boolean,
    ) => {
        const allowed = isCommandAllowed(command, state);
        const reasonKey = commandDisabledReasonKey(command, state);
        const Icon = ICONS[command];
        return (
            <button
                type="button"
                onClick={onClick}
                disabled={!allowed || busy}
                title={reasonKey ? t(reasonKey) : undefined}
                className="inline-flex items-center gap-1 rounded border border-slate-300 px-2 py-1 text-xs whitespace-nowrap text-slate-700 transition-colors hover:border-f1-red hover:text-f1-red disabled:cursor-not-allowed disabled:border-slate-300 disabled:text-slate-400 disabled:opacity-50 dark:border-slate-700 dark:text-slate-300 dark:hover:border-f1-red dark:hover:text-f1-red"
            >
                <Icon className="h-3.5 w-3.5" aria-hidden="true" />
                {t(labelKey)}
            </button>
        );
    };

    return (
        <div className="flex flex-wrap items-center gap-1.5 md:flex-nowrap">
            {button(
                'connect',
                'commands.connect',
                () => commands.connect.mutate(),
                commands.connect.isPending,
            )}
            {button(
                'startRun',
                'commands.startRun',
                () => {
                    // A previous attempt's error must not greet the freshly opened dialog.
                    commands.startRun.reset();
                    setShowStartDialog(true);
                },
                commands.startRun.isPending,
            )}
            {button(
                'disconnect',
                'commands.disconnect',
                () => commands.disconnect.mutate(),
                commands.disconnect.isPending,
            )}
            {button(
                'reset',
                'commands.reset',
                () => commands.reset.mutate(),
                commands.reset.isPending,
            )}
            {anyFailed && (
                <span role="alert" className="text-xs text-red-600 dark:text-red-400">
                    {t('commands.dispatchFailed')}
                </span>
            )}
            {showStartDialog && (
                <StartRunDialog
                    deviceGuid={deviceGuid}
                    startRun={commands.startRun}
                    onClose={() => setShowStartDialog(false)}
                />
            )}
        </div>
    );
}
