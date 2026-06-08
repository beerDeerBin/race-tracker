import { Link, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { PropsWithChildren, ReactNode } from 'react';
import { Flag, LogOut } from 'lucide-react';
import { ThemeToggle } from './ThemeToggle';
import { LanguageSwitcher } from './LanguageSwitcher';
import { useAuth } from '../hooks/useAuth';

/**
 * Shared protected-page chrome (header with app title, user, theme, sign-out) so the
 * detail pages don't re-implement the dashboard's shell. The root is transparent so the
 * app-wide racing background shows through; the header is an opaque carbon bar with an F1-red
 * accent underline.
 */
export function PageShell({ title, children }: PropsWithChildren<{ title: ReactNode }>) {
    const { t } = useTranslation();
    const { user, logout } = useAuth();
    const location = useLocation();

    return (
        <div className="flex flex-1 flex-col">
            <header className="flex flex-wrap items-center justify-between gap-3 border-b-2 border-f1-red bg-white/95 px-4 py-3 backdrop-blur sm:px-6 dark:bg-slate-900/95">
                <Link
                    to="/"
                    className="inline-flex items-center gap-2 text-xl font-semibold transition-colors hover:text-f1-red"
                >
                    <Flag className="h-5 w-5 text-f1-red" aria-hidden="true" />
                    {t('app.title')}
                </Link>
                <div className="flex flex-wrap items-center gap-2 text-sm sm:gap-4">
                    <span className="hidden text-slate-500 sm:inline dark:text-slate-400">
                        {t('dashboard.loggedInAs', { user })}
                    </span>
                    <LanguageSwitcher />
                    <ThemeToggle />
                    <button type="button" onClick={logout} className="btn-secondary">
                        <LogOut className="h-4 w-4" aria-hidden="true" />
                        {t('dashboard.logout')}
                    </button>
                </div>
            </header>
            <main
                key={location.pathname}
                className="mx-auto w-full max-w-[120rem] animate-page-in px-4 py-6 motion-reduce:animate-none sm:px-6"
            >
                <h2 className="mb-4 text-lg font-medium">{title}</h2>
                {children}
            </main>
        </div>
    );
}
