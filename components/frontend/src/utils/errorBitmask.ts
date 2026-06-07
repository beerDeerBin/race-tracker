/**
 * Decodes the device error-code bitmask (PROTOCOL §5.1) to i18n keys (/U30/). The mask is
 * a uint64; bits are tested with BigInt so a future high bit can't lose precision (a JS
 * number only stays exact below 2^53, and today's highest bit is 42). The caller
 * translates the returned keys — this util has no React/i18n dependency.
 */

export interface ErrorBit {
    bit: number;
    key: string;
}

/** Bit → i18n key, in PROTOCOL §5.1 order. */
export const ERROR_BITS: readonly ErrorBit[] = [
    { bit: 0, key: 'errors.eepromParameter' },
    { bit: 1, key: 'errors.eepromInit' },
    { bit: 2, key: 'errors.eepromWrite' },
    { bit: 8, key: 'errors.wifiInit' },
    { bit: 9, key: 'errors.wifiConnect' },
    { bit: 10, key: 'errors.wifiShutdown' },
    { bit: 11, key: 'errors.wifiWakeup' },
    { bit: 12, key: 'errors.wifiSleep' },
    { bit: 16, key: 'errors.damgrInit' },
    { bit: 17, key: 'errors.damgrAlloc' },
    { bit: 18, key: 'errors.damgrOverflow' },
    { bit: 24, key: 'errors.mqttConnect' },
    { bit: 25, key: 'errors.mqttPublish' },
    { bit: 26, key: 'errors.mqttSubscribe' },
    { bit: 32, key: 'errors.imuInit' },
    { bit: 33, key: 'errors.imuConfig' },
    { bit: 34, key: 'errors.imuRead' },
    { bit: 35, key: 'errors.imuFifo' },
    { bit: 40, key: 'errors.pwrInit' },
    { bit: 41, key: 'errors.pwrAdc' },
    { bit: 42, key: 'errors.pwrBatteryCritical' },
];

/** i18n keys of the set error bits, in table order; empty when no error is asserted. */
export function decodeErrorBits(errorCode: number | bigint | string): string[] {
    let mask: bigint;
    try {
        mask = BigInt(errorCode);
    } catch {
        return [];
    }
    return ERROR_BITS.filter(({ bit }) => (mask & (1n << BigInt(bit))) !== 0n).map(
        ({ key }) => key,
    );
}
