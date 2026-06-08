import { describe, expect, it, vi } from 'vitest';
import { GraphQlError, graphqlRequest } from './graphqlClient';
import { httpClient } from './httpClient';
import { config } from './config';

describe('graphqlRequest', () => {
    it('posts the query + variables to the persistence /graphql endpoint and unwraps data', async () => {
        const post = vi
            .spyOn(httpClient, 'post')
            .mockResolvedValueOnce({ data: { data: { runs: [] } } });

        const result = await graphqlRequest<{ runs: unknown[] }>('query Q { runs }', {
            deviceGuid: 'GUID-A',
        });

        expect(post).toHaveBeenCalledWith(`${config.persistenceUrl}/graphql`, {
            query: 'query Q { runs }',
            variables: { deviceGuid: 'GUID-A' },
        });
        expect(result).toEqual({ runs: [] });
    });

    it('throws a GraphQlError carrying the server messages on errors[]', async () => {
        vi.spyOn(httpClient, 'post').mockResolvedValueOnce({
            data: { errors: [{ message: 'boom' }, { message: 'bang' }] },
        });

        await expect(graphqlRequest('query Q { x }')).rejects.toThrowError(
            new GraphQlError(['boom', 'bang']),
        );
    });

    it('throws on an empty response body', async () => {
        vi.spyOn(httpClient, 'post').mockResolvedValueOnce({ data: {} });

        await expect(graphqlRequest('query Q { x }')).rejects.toBeInstanceOf(GraphQlError);
    });

    it('propagates HTTP-level failures untouched (the central 401 handling lives in httpClient)', async () => {
        const httpFailure = new Error('Network Error');
        vi.spyOn(httpClient, 'post').mockRejectedValueOnce(httpFailure);

        await expect(graphqlRequest('query Q { x }')).rejects.toBe(httpFailure);
    });
});
