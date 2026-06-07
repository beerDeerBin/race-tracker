import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

/**
 * Route guard (/U10/): nested routes render only for an authenticated user; everyone else
 * is redirected to the login page, remembering where they wanted to go.
 */
export function ProtectedRoute() {
    const { isAuthenticated } = useAuth();
    const location = useLocation();

    return isAuthenticated ? (
        <Outlet />
    ) : (
        <Navigate to="/login" replace state={{ from: location }} />
    );
}
