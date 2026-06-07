import { useQuery } from '@tanstack/react-query';
import { runService } from '../services/runService';

export const runsQueryKey = (deviceGuid: string) => ['runs', deviceGuid] as const;

/** A vehicle's runs (/F80/) — newest first for the list view. */
export function useRuns(deviceGuid: string) {
    return useQuery({
        queryKey: runsQueryKey(deviceGuid),
        queryFn: async () => {
            const runs = await runService.getRuns(deviceGuid);
            return [...runs].sort((a, b) => (b.startedAt ?? '').localeCompare(a.startedAt ?? ''));
        },
    });
}
