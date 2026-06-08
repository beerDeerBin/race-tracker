import { graphqlRequest } from '../utils/graphqlClient';
import type { Run } from '../models/graphql';

const RUNS_QUERY = `
    query Runs($deviceGuid: UUID!) {
        runs(deviceGuid: $deviceGuid) {
            deviceGuid
            runId
            numSamples
            odrHz
            accelRange
            gyroRange
            startedAt
            endedAt
            receivedSamples
        }
    }
`;

/** Run metadata of the persistence read path (/F80/, /F52/). */
export const runService = {
    async getRuns(deviceGuid: string): Promise<Run[]> {
        const data = await graphqlRequest<{ runs: Run[] }>(RUNS_QUERY, { deviceGuid });
        return data.runs;
    },
};
