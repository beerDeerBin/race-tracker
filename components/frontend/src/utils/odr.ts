import type { Run } from '../models/graphql';

/**
 * Time base of a run: t = index / odrHz (/F54/, no timestamp in the samples).
 *
 * DEFAULT_ODR_HZ mirrors the backend's PersistenceOptions.DefaultOdrHz fallback — run
 * metadata carries no ODR today (no producer supplies it; flagged systemic gap), so the
 * device default of 104 Hz applies. Keep the two constants in sync.
 */
export const DEFAULT_ODR_HZ = 104;

export function effectiveOdrHz(run: Pick<Run, 'odrHz'> | null | undefined): number {
    const odrHz = run?.odrHz;
    return odrHz != null && odrHz > 0 ? odrHz : DEFAULT_ODR_HZ;
}

/** Seconds since run start for an absolute sample index. */
export function timeOfIndex(index: number, odrHz: number): number {
    return index / odrHz;
}
