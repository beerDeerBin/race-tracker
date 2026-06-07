import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { PropsWithChildren, ReactNode } from 'react';
import { ThemeToggle } from './ThemeToggle';
import { useAuth } from '../hooks/useAuth';

/**
 * Shared protected-page chrome (header with app title, user, theme, sign-out) so the
 * detail pages don't re-implement the dashboard's shell.
 */
export function PageShell({ title, children }: PropsWithChildren<{ title: ReactNode }>) {
    const { t } = useTranslation();
    const { user, logout } = useAuth();

    return (
        <div className="min-h-screen bg-slate-100 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
            <header className="flex items-center justify-between border-b border-slate-200 bg-white px-6 py-4 dark:border-slate-800 dark:bg-slate-900">
                <Link to="/" className="text-xl font-semibold hover:text-sky-600">
                    {t('app.title')}
                </Link>
                <div className="flex items-center gap-4 text-sm">
                    <span className="text-slate-500 dark:text-slate-400">
                        {t('dashboard.loggedInAs', { user })}
                    </span>
                    <ThemeToggle />
                    <button
                        type="button"
                        onClick={logout}
                        className="rounded border border-slate-300 px-3 py-1.5 hover:bg-slate-200 dark:border-slate-700 dark:hover:bg-slate-800"
                    >
                        {t('dashboard.logout')}
                    </button>
                </div>
            </header>
            <main className="p-6">
                <h2 className="mb-4 text-lg font-medium">{title}</h2>
                {children}
            </main>
        </div>
    );
}
