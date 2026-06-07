import { httpClient } from './httpClient';
import { config } from './config';

/**
 * Lightweight typed GraphQL transport over the single HTTP instance (/U50/): bearer
 * injection and the central 401 → logout apply to GraphQL exactly like to REST. No
 * client-side cache layer — React Query owns server state.
 */

interface GraphQlResponse<T> {
    data?: T;
    errors?: Array<{ message: string }>;
}

/** A GraphQL-level error (HTTP 200 with an errors array). */
export class GraphQlError extends Error {
    constructor(messages: string[]) {
        super(messages.join('; '));
        this.name = 'GraphQlError';
    }
}

export async function graphqlRequest<T>(
    query: string,
    variables?: Record<string, unknown>,
): Promise<T> {
    const { data } = await httpClient.post<GraphQlResponse<T>>(`${config.persistenceUrl}/graphql`, {
        query,
        variables,
    });

    // Deliberately all-or-nothing: a response carrying both data and errors (GraphQL
    // partial success) still throws — this read API has no partial-result consumers.
    if (data.errors && data.errors.length > 0) {
        throw new GraphQlError(data.errors.map((error) => error.message));
    }
    if (data.data === undefined) {
        throw new GraphQlError(['Empty GraphQL response']);
    }
    return data.data;
}
