import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { PropsWithChildren } from 'react';
import { TrajectoryPage } from './TrajectoryPage';
import { AuthContext } from '../context/AuthContext';
import { ThemeProvider } from '../context/ThemeProvider';
import { useTrajectory } from '../hooks/useTrajectory';
import type { TrajectoryPoint } from '../models/graphql';

vi.mock('../hooks/useTrajectory', () => ({ useTrajectory: vi.fn() }));
vi.mock('../components/TrajectoryMap', () => ({
    TrajectoryMap: () => <div data-testid="trajectory-map" />,
}));

const useTrajectoryMock = vi.mocked(useTrajectory);
type Result = ReturnType<typeof useTrajectory>;

const points: TrajectoryPoint[] = [
    { index: 0, t: 0, x: 0, y: 0, heading: 0 },
    { index: 1, t: 1, x: 1, y: 1, heading: 0 },
];

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
            <MemoryRouter initialEntries={['/vehicles/GUID-Aa/runs/run-1/trajectory']}>
                <AuthContext.Provider value={auth}>
                    <Routes>
                        <Route
                            path="/vehicles/:deviceGuid/runs/:runId/trajectory"
                            element={children}
                        />
                    </Routes>
                </AuthContext.Provider>
            </MemoryRouter>
        </ThemeProvider>
    );
}

describe('TrajectoryPage', () => {
    it('renders the map and playback controls on success', () => {
        useTrajectoryMock.mockReturnValue({
            data: points,
            isPending: false,
            isError: false,
        } as unknown as Result);

        render(
            <Providers>
                <TrajectoryPage />
            </Providers>,
        );

        expect(screen.getByTestId('trajectory-map')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Play' })).toBeInTheDocument();
        expect(screen.getByRole('slider', { name: 'Playback position' })).toBeInTheDocument();
    });

    it('shows the empty state for a run without a trajectory', () => {
        useTrajectoryMock.mockReturnValue({
            data: [],
            isPending: false,
            isError: false,
        } as unknown as Result);

        render(
            <Providers>
                <TrajectoryPage />
            </Providers>,
        );

        expect(screen.getByText('This run has no trajectory.')).toBeInTheDocument();
        expect(screen.queryByTestId('trajectory-map')).not.toBeInTheDocument();
    });

    it('shows the error state', () => {
        useTrajectoryMock.mockReturnValue({
            data: undefined,
            isPending: false,
            isError: true,
        } as unknown as Result);

        render(
            <Providers>
                <TrajectoryPage />
            </Providers>,
        );

        expect(screen.getByRole('alert')).toHaveTextContent('Could not load the trajectory.');
    });
});
