import { useState } from 'react';
import type { FormEvent } from 'react';
import { useLocation, useNavigate, Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { InvalidCredentialsError } from '../services/authService';
import { useAuth } from '../hooks/useAuth';
import { ThemeToggle } from '../components/ThemeToggle';

/** Login view (/U10/, /U20/): authenticates against the real management service. */
export function LoginPage() {
    const { t } = useTranslation();
    const { isAuthenticated, login } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();

    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [submitting, setSubmitting] = useState(false);
    const [errorKey, setErrorKey] = useState<string | null>(null);

    // Already signed in (e.g. reload on /login with a live session) — go straight in.
    if (isAuthenticated) {
        const from = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname;
        return <Navigate to={from ?? '/'} replace />;
    }

    const handleSubmit = async (event: FormEvent) => {
        event.preventDefault();
        setSubmitting(true);
        setErrorKey(null);
        try {
            await login(username, password);
            const from = (location.state as { from?: { pathname?: string } } | null)?.from
                ?.pathname;
            navigate(from ?? '/', { replace: true });
        } catch (error) {
            setErrorKey(
                error instanceof InvalidCredentialsError
                    ? 'login.invalidCredentials'
                    : 'login.failed',
            );
        } finally {
            setSubmitting(false);
        }
    };

    const inputClasses =
        'w-full rounded border border-slate-300 bg-white px-3 py-2 text-slate-900 focus:border-sky-500 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100';

    return (
        <div className="relative flex min-h-screen items-center justify-center bg-slate-100 p-4 dark:bg-slate-950">
            <div className="absolute top-4 right-4">
                <ThemeToggle />
            </div>
            <form
                onSubmit={handleSubmit}
                className="w-full max-w-sm space-y-4 rounded-lg bg-white p-8 shadow-xl dark:bg-slate-900"
            >
                <h1 className="text-center text-2xl font-semibold text-slate-900 dark:text-slate-100">
                    {t('app.title')}
                </h1>
                <p className="text-center text-sm text-slate-500 dark:text-slate-400">
                    {t('login.title')}
                </p>

                <label className="block">
                    <span className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">
                        {t('login.username')}
                    </span>
                    <input
                        type="text"
                        value={username}
                        onChange={(event) => setUsername(event.target.value)}
                        autoComplete="username"
                        required
                        className={inputClasses}
                    />
                </label>

                <label className="block">
                    <span className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">
                        {t('login.password')}
                    </span>
                    <input
                        type="password"
                        value={password}
                        onChange={(event) => setPassword(event.target.value)}
                        autoComplete="current-password"
                        required
                        className={inputClasses}
                    />
                </label>

                {errorKey && (
                    <p role="alert" className="text-sm text-red-600 dark:text-red-400">
                        {t(errorKey)}
                    </p>
                )}

                <button
                    type="submit"
                    disabled={submitting}
                    className="w-full rounded bg-sky-600 px-4 py-2 font-medium text-white hover:bg-sky-500 disabled:cursor-not-allowed disabled:opacity-50"
                >
                    {submitting ? t('login.submitting') : t('login.submit')}
                </button>
            </form>
        </div>
    );
}
