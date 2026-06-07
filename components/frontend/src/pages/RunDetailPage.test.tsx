import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { PropsWithChildren } from 'react';
import { RunDetailPage } from './RunDetailPage';
import { AuthContext } from '../context/AuthContext';
import { ThemeProvider } from '../context/ThemeProvider';
import { useRuns } from '../hooks/useRuns';
import { useSamples } from '../hooks/useSamples';
import { useRunRollup } from '../hooks/useRunRollup';
import type { Sample } from '../models/graphql';

vi.mock('../hooks/useRuns', () => ({ useRuns: vi.fn() }));
vi.mock('../hooks/useSamples', () => ({ useSamples: vi.fn() }));
vi.mock('../hooks/useRunRollup', () => ({ useRunRollup: vi.fn() }));
// Live append is exercised in useLiveRun.test; here it must not touch the network.
vi.mock('../hooks/useLiveRun', () => ({ useLiveRun: vi.fn() }));
// The chart itself is uPlot/canvas territory — replace it with a recording marker.
const chartProps = vi.hoisted(() => [] as Array<Record<string, unknown>>);
vi.mock('../components/AxisChart', () => ({
    AxisChart: (props: { title: string; unit: string }) => {
        chartProps.push(props);
        return (
            <div data-testid="axis-chart">
                {props.title} [{props.unit}]
            </div>
        );
    },
}));

const useRunsMock = vi.mocked(useRuns);
const useSamplesMock = vi.mocked(useSamples);
const useRunRollupMock = vi.mocked(useRunRollup);
type RollupResult = ReturnType<typeof useRunRollup>;

function idleRollup(): RollupResult {
    return {
        data: undefined,
        isPending: false,
        isError: false,
        isSuccess: false,
    } as unknown as RollupResult;
}

const samples: Sample[] = [{ index: 0, ax: 1, ay: 2, az: 3, gx: 4, gy: 5, gz: 6 }];

function Providers({ children }: PropsWithChildren) {
    const auth = {
        user: 'admin',
        role: 'admin',
        isAuthenticated: true,
        login: vi.fn(),
        logout: vi.fn(),
    };
    return (
        <ThemeProvider>
            <MemoryRouter initialEntries={['/vehicles/GUID-Aa/runs/run-1']}>
                <AuthContext.Provider value={auth}>
                    <Routes>
                        <Route path="/vehicles/:deviceGuid/runs/:runId" element={children} />
                    </Routes>
                </AuthContext.Provider>
            </MemoryRouter>
        </ThemeProvider>
    );
}

type RunsResult = ReturnType<typeof useRuns>;
type SamplesResult = ReturnType<typeof useSamples>;

describe('RunDetailPage', () => {
    beforeEach(() => {
        chartProps.length = 0;
        useRunRollupMock.mockReturnValue(idleRollup());
    });

    it('renders both six-axis charts with their units once samples arrive', () => {
        useRunsMock.mockReturnValue({ data: [] } as unknown as RunsResult);
        useSamplesMock.mockReturnValue({
            data: samples,
            isPending: false,
            isError: false,
        } as unknown as SamplesResult);

        render(
            <Providers>
                <RunDetailPage />
            </Providers>,
        );

        const charts = screen.getAllByTestId('axis-chart');
        expect(charts).toHaveLength(2);
        expect(charts[0]).toHaveTextContent('Acceleration ax / ay / az [m/s²]');
        expect(charts[1]).toHaveTextContent('Angular velocity gx / gy / gz [rad/s]');
    });

    it('shows the error state when loading fails', () => {
        useRunsMock.mockReturnValue({ data: [] } as unknown as RunsResult);
        useSamplesMock.mockReturnValue({
            data: undefined,
            isPending: false,
            isError: true,
        } as unknown as SamplesResult);

        render(
            <Providers>
                <RunDetailPage />
            </Providers>,
        );

        expect(screen.getByRole('alert')).toHaveTextContent('Could not load the samples.');
        expect(screen.queryAllByTestId('axis-chart')).toHaveLength(0);
    });

    it('switches to the aggregate roll-up view with bands (7.6)', async () => {
        useRunsMock.mockReturnValue({ data: [] } as unknown as RunsResult);
        useSamplesMock.mockReturnValue({
            data: samples,
            isPending: false,
            isError: false,
        } as unknown as SamplesResult);
        const axis = { min: 0, max: 1, avg: 0.5 };
        useRunRollupMock.mockReturnValue({
            data: [
                {
                    bucketStartIndex: 0,
                    ax: axis,
                    ay: axis,
                    az: axis,
                    gx: axis,
                    gy: axis,
                    gz: axis,
                    sampleCount: 100,
                },
            ],
            isPending: false,
            isError: false,
            isSuccess: true,
        } as unknown as RollupResult);

        render(
            <Providers>
                <RunDetailPage />
            </Providers>,
        );
        await userEvent.click(screen.getByRole('button', { name: 'Aggregate' }));

        const lastAccel = chartProps.at(-2)!;
        expect((lastAccel.series as unknown[]).length).toBe(9);
        expect(lastAccel.bands).toBeDefined();
        // min/max/avg columns + t for one bucket
        expect((lastAccel.data as unknown[]).length).toBe(10);
    });

    it('time-range filter shrinks the data handed to the charts (7.6)', async () => {
        useRunsMock.mockReturnValue({ data: [] } as unknown as RunsResult);
        const manySamples = [0, 52, 104, 208].map((index) => ({
            index,
            ax: 0,
            ay: 0,
            az: 0,
            gx: 0,
            gy: 0,
            gz: 0,
        }));
        useSamplesMock.mockReturnValue({
            data: manySamples,
            isPending: false,
            isError: false,
        } as unknown as SamplesResult);

        render(
            <Providers>
                <RunDetailPage />
            </Providers>,
        );
        await userEvent.type(screen.getByLabelText('From [s]'), '1');

        const lastAccel = chartProps.at(-2)!;
        const t = (lastAccel.data as number[][])[0]!;
        // Only indices 104 and 208 (t >= 1 s at 104 Hz) survive the filter.
        expect(t).toHaveLength(2);
    });

    it('shows the empty message instead of blank axes when the filter excludes everything', async () => {
        useRunsMock.mockReturnValue({ data: [] } as unknown as RunsResult);
        useSamplesMock.mockReturnValue({
            data: samples,
            isPending: false,
            isError: false,
        } as unknown as SamplesResult);

        render(
            <Providers>
                <RunDetailPage />
            </Providers>,
        );
        await userEvent.type(screen.getByLabelText('From [s]'), '999');

        expect(screen.getByText('This run has no samples.')).toBeInTheDocument();
        expect(screen.queryAllByTestId('axis-chart')).toHaveLength(0);
    });
});
