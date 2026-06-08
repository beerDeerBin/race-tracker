import { useState } from 'react';
import type { FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { UseMutationResult } from '@tanstack/react-query';
import type {
    AccelRange,
    GyroRange,
    ImuOdr,
    StartRunRequest,
    StartRunResponse,
} from '../models/api';

/** Backend NumSamples is a uint — values above this are rejected with a 400. */
const MAX_NUM_SAMPLES = 4_294_967_295;

const ODR_VALUES: ImuOdr[] = ['hz12_5', 'hz26', 'hz52', 'hz104', 'hz208', 'hz417', 'hz833'];
const ACCEL_VALUES: AccelRange[] = ['g2', 'g4', 'g8', 'g16'];
const GYRO_VALUES: GyroRange[] = ['dps125', 'dps250', 'dps500', 'dps1000', 'dps2000'];

const ODR_LABELS: Record<ImuOdr, string> = {
    hz12_5: '12.5 Hz',
    hz26: '26 Hz',
    hz52: '52 Hz',
    hz104: '104 Hz',
    hz208: '208 Hz',
    hz417: '417 Hz',
    hz833: '833 Hz',
};
const ACCEL_LABELS: Record<AccelRange, string> = {
    g2: '±2 g',
    g4: '±4 g',
    g8: '±8 g',
    g16: '±16 g',
};
const GYRO_LABELS: Record<GyroRange, string> = {
    dps125: '±125 dps',
    dps250: '±250 dps',
    dps500: '±500 dps',
    dps1000: '±1000 dps',
    dps2000: '±2000 dps',
};

/**
 * Run parameter dialog (/F31/, /D40/): numSamples + ODR + IMU ranges, defaults matching
 * the device defaults (104 Hz, ±4 g, ±500 dps). Submit dispatches START_RUN; progress
 * then arrives via the 6.3 push.
 */
export function StartRunDialog({
    deviceGuid,
    startRun,
    onClose,
}: {
    deviceGuid: string;
    startRun: UseMutationResult<StartRunResponse, Error, StartRunRequest>;
    onClose: () => void;
}) {
    const { t } = useTranslation();
    const [numSamples, setNumSamples] = useState('1000');
    const [odr, setOdr] = useState<ImuOdr>('hz104');
    const [accelRange, setAccelRange] = useState<AccelRange>('g4');
    const [gyroRange, setGyroRange] = useState<GyroRange>('dps500');

    const handleSubmit = (event: FormEvent) => {
        event.preventDefault();
        const samples = Number(numSamples);
        if (!Number.isInteger(samples) || samples < 1 || samples > MAX_NUM_SAMPLES) {
            return;
        }
        startRun.mutate(
            { numSamples: samples, odr, accelRange, gyroRange },
            { onSuccess: onClose },
        );
    };

    const inputClasses =
        'w-full rounded border border-slate-300 bg-white px-3 py-2 text-slate-900 focus:border-sky-500 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100';
    const labelClasses = 'mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300';

    return (
        <div
            className="fixed inset-0 z-10 flex items-center justify-center bg-black/40 p-4"
            role="presentation"
            onClick={onClose}
        >
            <form
                role="dialog"
                aria-modal="true"
                aria-labelledby="start-run-dialog-title"
                onClick={(event) => event.stopPropagation()}
                onKeyDown={(event) => {
                    if (event.key === 'Escape') {
                        onClose();
                    }
                }}
                onSubmit={handleSubmit}
                className="w-full max-w-sm space-y-4 rounded-lg bg-white p-6 shadow-xl dark:bg-slate-900"
            >
                <h2
                    id="start-run-dialog-title"
                    className="text-lg font-semibold text-slate-900 dark:text-slate-100"
                >
                    {t('commands.startRunTitle')}
                </h2>
                <p className="font-mono text-xs break-all text-slate-400 dark:text-slate-500">
                    {deviceGuid}
                </p>

                <label className="block">
                    <span className={labelClasses}>{t('commands.numSamples')}</span>
                    <input
                        type="number"
                        min={1}
                        max={MAX_NUM_SAMPLES}
                        step={1}
                        value={numSamples}
                        onChange={(event) => setNumSamples(event.target.value)}
                        required
                        autoFocus
                        className={inputClasses}
                    />
                </label>

                <label className="block">
                    <span className={labelClasses}>{t('commands.odr')}</span>
                    <select
                        value={odr}
                        onChange={(event) => setOdr(event.target.value as ImuOdr)}
                        className={inputClasses}
                    >
                        {ODR_VALUES.map((value) => (
                            <option key={value} value={value}>
                                {ODR_LABELS[value]}
                            </option>
                        ))}
                    </select>
                </label>

                <label className="block">
                    <span className={labelClasses}>{t('commands.accelRange')}</span>
                    <select
                        value={accelRange}
                        onChange={(event) => setAccelRange(event.target.value as AccelRange)}
                        className={inputClasses}
                    >
                        {ACCEL_VALUES.map((value) => (
                            <option key={value} value={value}>
                                {ACCEL_LABELS[value]}
                            </option>
                        ))}
                    </select>
                </label>

                <label className="block">
                    <span className={labelClasses}>{t('commands.gyroRange')}</span>
                    <select
                        value={gyroRange}
                        onChange={(event) => setGyroRange(event.target.value as GyroRange)}
                        className={inputClasses}
                    >
                        {GYRO_VALUES.map((value) => (
                            <option key={value} value={value}>
                                {GYRO_LABELS[value]}
                            </option>
                        ))}
                    </select>
                </label>

                {startRun.isError && (
                    <p role="alert" className="text-sm text-red-600 dark:text-red-400">
                        {t('commands.startFailed')}
                    </p>
                )}

                <div className="flex justify-end gap-2">
                    <button
                        type="button"
                        onClick={onClose}
                        className="rounded border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
                    >
                        {t('commands.cancel')}
                    </button>
                    <button
                        type="submit"
                        disabled={startRun.isPending}
                        className="rounded bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                        {startRun.isPending ? t('commands.starting') : t('commands.start')}
                    </button>
                </div>
            </form>
        </div>
    );
}
