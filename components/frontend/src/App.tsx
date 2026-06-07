import { Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './components/ProtectedRoute';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { VehicleDetailPage } from './pages/VehicleDetailPage';
import { RunDetailPage } from './pages/RunDetailPage';
import { TrajectoryPage } from './pages/TrajectoryPage';

/** Route tree (/U10/): everything except /login sits behind the auth guard. */
function App() {
    return (
        <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route element={<ProtectedRoute />}>
                <Route path="/" element={<DashboardPage />} />
                <Route path="/vehicles/:deviceGuid" element={<VehicleDetailPage />} />
                <Route path="/vehicles/:deviceGuid/runs/:runId" element={<RunDetailPage />} />
                <Route
                    path="/vehicles/:deviceGuid/runs/:runId/trajectory"
                    element={<TrajectoryPage />}
                />
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
    );
}

export default App;
