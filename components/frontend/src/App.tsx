import { Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './components/ProtectedRoute';
import { RacingBackground } from './components/RacingBackground';
import { Footer } from './components/Footer';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { VehicleDetailPage } from './pages/VehicleDetailPage';
import { RunDetailPage } from './pages/RunDetailPage';
import { TrajectoryPage } from './pages/TrajectoryPage';

/**
 * Route tree (/U10/): everything except /login sits behind the auth guard. The app-wide chrome —
 * the fixed racing background and the footer — lives here so it shows on every route; pages render
 * in the growing middle region and stay transparent so the background shows through.
 */
function App() {
    return (
        <div className="relative flex min-h-dvh flex-col">
            <RacingBackground />
            {/* The page-enter animation lives on each page's <main> (PageShell) / login form, so the
                shared header and footer stay solid across navigations. */}
            <div className="flex flex-1 flex-col">
                <Routes>
                    <Route path="/login" element={<LoginPage />} />
                    <Route element={<ProtectedRoute />}>
                        <Route path="/" element={<DashboardPage />} />
                        <Route path="/vehicles/:deviceGuid" element={<VehicleDetailPage />} />
                        <Route
                            path="/vehicles/:deviceGuid/runs/:runId"
                            element={<RunDetailPage />}
                        />
                        <Route
                            path="/vehicles/:deviceGuid/runs/:runId/trajectory"
                            element={<TrajectoryPage />}
                        />
                    </Route>
                    <Route path="*" element={<Navigate to="/" replace />} />
                </Routes>
            </div>
            <Footer />
        </div>
    );
}

export default App;
