import { graphqlRequest } from '../utils/graphqlClient';
import type { SampleRollupBucket } from '../models/graphql';

const ROLLUP_QUERY = `
    query RunRollup($runId: UUID!, $limit: Int) {
        runRollup(runId: $runId, limit: $limit) {
            bucketStartIndex
            ax { min max avg }
            ay { min max avg }
            az { min max avg }
            gx { min max avg }
            gy { min max avg }
            gz { min max avg }
            sampleCount
        }
    }
`;

/**
 * Pre-computed roll-up buckets of the 4.2 continuous aggregate (/F53/). Buckets fold 100
 * raw samples each, so even the 200k raw cap yields at most 2k buckets — a single page
 * inside the server's 50k clamp always suffices.
 */
export const rollupService = {
    async getRunRollup(runId: string): Promise<SampleRollupBucket[]> {
        const data = await graphqlRequest<{ runRollup: SampleRollupBucket[] }>(ROLLUP_QUERY, {
            runId,
            limit: 50_000,
        });
        return data.runRollup;
    },
};
