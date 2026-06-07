import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { PropsWithChildren } from 'react';
import { RunDetailPage } from './RunDetailPage';
import { AuthContext } from '../context/AuthContext';
import { ThemeProvider } from '../context/ThemeProvider';
import { useRuns } from '../hooks/useRuns';
import { useSamples } from '../hooks/useSamples';
import type { Sample } from '../models/graphql';

vi.mock('../hooks/useRuns', () => ({ useRuns: vi.fn() }));
vi.mock('../hooks/useSamples', () => ({ useSamples: vi.fn() }));
// The chart itself is uPlot/canvas territory — replace it with a marker.
vi.mock('../components/AxisChart', () => ({
    AxisChart: ({ title, unit }: { title: string; unit: string }) => (
        <div data-testid="axis-chart">
            {title} [{unit}]
        </div>
    ),
}));

const useRunsMock = vi.mocked(useRuns);
const useSamplesMock = vi.mocked(useSamples);

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
});
