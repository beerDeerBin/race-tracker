/** Display formatting for the live status fields (/F60/, /D30/) — pure and unit-tested. */

const BATTERY_MV_UNKNOWN = 65535;
const BATTERY_PCT_UNKNOWN = 255;

/** "3987 mV · 76 %" with the protocol's unknown sentinels rendered as "—". */
export function formatBattery(batteryMv: number, batteryPct: number): string {
    const mv = batteryMv === BATTERY_MV_UNKNOWN ? '—' : `${batteryMv} mV`;
    const pct = batteryPct === BATTERY_PCT_UNKNOWN ? '—' : `${batteryPct} %`;
    return `${mv} · ${pct}`;
}

/** Compact uptime: "47s", "12m 05s", "3h 07m". */
export function formatUptime(uptimeMs: number): string {
    const totalSeconds = Math.floor(uptimeMs / 1000);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;

    if (hours > 0) {
        return `${hours}h ${String(minutes).padStart(2, '0')}m`;
    }
    if (minutes > 0) {
        return `${minutes}m ${String(seconds).padStart(2, '0')}s`;
    }
    return `${seconds}s`;
}

/** Seconds elapsed since an ISO timestamp (clamped at 0, e.g. for clock skew). */
export function secondsSince(isoTimestamp: string, now: Date = new Date()): number {
    return Math.max(0, Math.floor((now.getTime() - new Date(isoTimestamp).getTime()) / 1000));
}
