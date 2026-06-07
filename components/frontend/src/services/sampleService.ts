import { graphqlRequest } from '../utils/graphqlClient';
import { logger } from '../utils/logger';
import type { Sample } from '../models/graphql';

const SAMPLES_QUERY = `
    query Samples($runId: UUID!, $fromIndex: Long, $limit: Int) {
        samples(runId: $runId, fromIndex: $fromIndex, limit: $limit) {
            index
            ax
            ay
            az
            gx
            gy
            gz
        }
    }
`;

/** The server clamps page sizes to this maximum. */
const PAGE_SIZE = 50_000;

/** Hard in-memory cap; beyond it the chart should use the 7.6 roll-up view anyway. */
const MAX_POINTS = 200_000;

/** Raw samples of the persistence read path (/F81/, /F52/). */
export const sampleService = {
    /** One page, used by the live tail in 7.7. */
    async getSamples(runId: string, fromIndex: number, limit: number): Promise<Sample[]> {
        const data = await graphqlRequest<{ samples: Sample[] }>(SAMPLES_QUERY, {
            runId,
            fromIndex,
            limit,
        });
        return data.samples;
    },

    /** The full run, assembled through the server's page-size clamp. */
    async getAllSamples(runId: string): Promise<Sample[]> {
        const all: Sample[] = [];
        let fromIndex = 0;

        for (;;) {
            const page = await sampleService.getSamples(runId, fromIndex, PAGE_SIZE);
            all.push(...page);

            if (page.length < PAGE_SIZE) {
                return all;
            }
            if (all.length >= MAX_POINTS) {
                logger.warn('Sample fetch capped — switch to the aggregate view for this run', {
                    runId,
                    fetched: all.length,
                });
                return all;
            }
            fromIndex = page[page.length - 1]!.index + 1;
        }
    },
};
