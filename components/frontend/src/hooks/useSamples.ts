import { useQuery } from '@tanstack/react-query';
import { sampleService } from '../services/sampleService';

export const samplesQueryKey = (runId: string) => ['samples', runId] as const;

/** All samples of one run (/F81/), assembled through the server's paging clamp. */
export function useSamples(runId: string) {
    return useQuery({
        queryKey: samplesQueryKey(runId),
        queryFn: () => sampleService.getAllSamples(runId),
        // A finished run's data is immutable — no point refetching on focus.
        staleTime: Infinity,
    });
}
