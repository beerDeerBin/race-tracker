import { useQuery } from '@tanstack/react-query';
import { rollupService } from '../services/rollupService';

export const rollupQueryKey = (runId: string) => ['rollup', runId] as const;

/** The 4.2 roll-up buckets of one run (/F53/) for the aggregate chart view. */
export function useRunRollup(runId: string, enabled = true) {
    return useQuery({
        queryKey: rollupQueryKey(runId),
        queryFn: () => rollupService.getRunRollup(runId),
        // A finished run's aggregate is immutable.
        staleTime: Infinity,
        enabled,
    });
}
