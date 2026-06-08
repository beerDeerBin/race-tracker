import { graphqlRequest } from '../utils/graphqlClient';
import type { TrajectoryPoint } from '../models/graphql';

const TRAJECTORY_QUERY = `
    query Trajectory($runId: UUID!, $stride: Int, $limit: Int) {
        trajectory(runId: $runId, stride: $stride, limit: $limit) {
            index
            t
            x
            y
            heading
        }
    }
`;

/** The server clamps trajectory page size to this maximum. */
const LIMIT = 100_000;

/**
 * Dead-reckoned 2D trajectory of a run (4.3, /F52/) — a pre-computed ordered point
 * sequence. Pass `stride` to downsample very large runs (the server always includes
 * index 0).
 */
export const trajectoryService = {
    async getTrajectory(runId: string, stride?: number): Promise<TrajectoryPoint[]> {
        const data = await graphqlRequest<{ trajectory: TrajectoryPoint[] }>(TRAJECTORY_QUERY, {
            runId,
            stride: stride ?? null,
            limit: LIMIT,
        });
        return data.trajectory;
    },
};
