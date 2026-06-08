import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { RunList } from './RunList';
import type { Run } from '../models/graphql';

function run(overrides: Partial<Run>): Run {
    return {
        deviceGuid: 'GUID-Aa',
        runId: 'run-1',
        numSamples: null,
        odrHz: null,
        accelRange: null,
        gyroRange: null,
        startedAt: '2026-06-07T17:28:09Z',
        endedAt: '2026-06-07T17:28:14Z',
        receivedSamples: 500,
        ...overrides,
    };
}

describe('RunList', () => {
    it('links each run with the verbatim route guid, not the lower-cased GraphQL run.deviceGuid', () => {
        // Persistence returns the guid lower-cased (PostgreSQL uuid column); the realtime
        // SignalR group is the verbatim upper-case key. The link must carry the verbatim guid
        // so the run-detail live tail (7.7) subscribes to the group realtime actually pushes to.
        render(
            <MemoryRouter>
                <RunList deviceGuid="GUID-Aa" runs={[run({ deviceGuid: 'guid-aa' })]} />
            </MemoryRouter>,
        );

        expect(screen.getByRole('link', { name: 'Open' })).toHaveAttribute(
            'href',
            '/vehicles/GUID-Aa/runs/run-1',
        );
    });

    it('marks the assumed sample rate when run metadata carries none', () => {
        render(
            <MemoryRouter>
                <RunList deviceGuid="GUID-Aa" runs={[run({ odrHz: null })]} />
            </MemoryRouter>,
        );

        expect(screen.getByText('104 Hz (assumed)')).toBeInTheDocument();
    });

    it('shows the real sample rate and requested count when present', () => {
        render(
            <MemoryRouter>
                <RunList deviceGuid="GUID-Aa" runs={[run({ odrHz: 208, numSamples: 1000 })]} />
            </MemoryRouter>,
        );

        expect(screen.getByText('208 Hz')).toBeInTheDocument();
        expect(screen.getByText('500 / 1000')).toBeInTheDocument();
    });
});
