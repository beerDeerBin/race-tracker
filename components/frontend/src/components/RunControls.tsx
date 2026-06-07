import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { StartRunDialog } from './StartRunDialog';
import { useCommands } from '../hooks/useCommands';
import { commandDisabledReasonKey, isCommandAllowed } from '../utils/commandValidity';
import type { DeviceCommand } from '../utils/commandValidity';
import type { DeviceState } from '../models/realtime';

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
        return (
            <button
                type="button"
                onClick={onClick}
                disabled={!allowed || busy}
                title={reasonKey ? t(reasonKey) : undefined}
                className="rounded border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-40 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
            >
                {t(labelKey)}
            </button>
        );
    };

    return (
        <div className="flex flex-wrap items-center gap-1.5">
            {button(
                'connect',
                'commands.connect',
                () => commands.connect.mutate(),
                commands.connect.isPending,
            )}
            {button(
                'startRun',
                'commands.startRun',
                () => setShowStartDialog(true),
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
