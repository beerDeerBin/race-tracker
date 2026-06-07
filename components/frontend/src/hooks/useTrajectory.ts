import { useQuery } from '@tanstack/react-query';
import { trajectoryService } from '../services/trajectoryService';

export const trajectoryQueryKey = (runId: string) => ['trajectory', runId] as const;

/** A run's dead-reckoned 2D path (4.3) for the map + playback. */
export function useTrajectory(runId: string) {
    return useQuery({
        queryKey: trajectoryQueryKey(runId),
        queryFn: () => trajectoryService.getTrajectory(runId),
        // A finished run's trajectory is immutable.
        staleTime: Infinity,
    });
}
