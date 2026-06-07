import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './ProtectedRoute';
import { AuthProvider } from '../context/AuthProvider';
import { tokenStore } from '../utils/tokenStore';

// The provider confirms sessions via /me; keep the guard test off the network.
vi.mock('../services/authService', () => ({
    authService: {
        login: vi.fn(),
        me: vi.fn().mockResolvedValue({ username: 'admin', role: 'admin' }),
    },
}));

function renderGuardedApp() {
    render(
        <MemoryRouter initialEntries={['/']}>
            <AuthProvider>
                <Routes>
                    <Route path="/login" element={<div>login page</div>} />
                    <Route element={<ProtectedRoute />}>
                        <Route path="/" element={<div>protected content</div>} />
                    </Route>
                </Routes>
            </AuthProvider>
        </MemoryRouter>,
    );
}

describe('ProtectedRoute', () => {
    it('redirects unauthenticated users to the login page', () => {
        renderGuardedApp();

        expect(screen.getByText('login page')).toBeInTheDocument();
        expect(screen.queryByText('protected content')).not.toBeInTheDocument();
    });

    it('renders the protected content for an authenticated user', () => {
        tokenStore.set({
            accessToken: 'live',
            expiresAt: '2099-01-01T00:00:00+00:00',
            username: 'admin',
        });

        renderGuardedApp();

        expect(screen.getByText('protected content')).toBeInTheDocument();
        expect(screen.queryByText('login page')).not.toBeInTheDocument();
    });
});
