import { Component } from 'react';
import type { ErrorInfo, PropsWithChildren } from 'react';
import { useTranslation } from 'react-i18next';
import { logger } from '../utils/logger';

/**
 * Error boundary (/U50/): contains render-time failures instead of white-screening the
 * whole SPA. Class component because boundaries have no hook equivalent; the fallback is
 * a function component so it can use the i18n hook.
 */

function ErrorFallback() {
    const { t } = useTranslation();
    return (
        <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-100 p-8 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
            <h1 className="text-2xl font-semibold">{t('error.title')}</h1>
            <p className="text-slate-500 dark:text-slate-400">{t('error.description')}</p>
            <button
                type="button"
                onClick={() => window.location.reload()}
                className="rounded bg-sky-600 px-4 py-2 font-medium hover:bg-sky-500"
            >
                {t('error.reload')}
            </button>
        </div>
    );
}

interface ErrorBoundaryState {
    hasError: boolean;
}

export class ErrorBoundary extends Component<PropsWithChildren, ErrorBoundaryState> {
    state: ErrorBoundaryState = { hasError: false };

    static getDerivedStateFromError(): ErrorBoundaryState {
        return { hasError: true };
    }

    componentDidCatch(error: Error, info: ErrorInfo): void {
        logger.error('Unhandled render error', { error, componentStack: info.componentStack });
    }

    render() {
        return this.state.hasError ? <ErrorFallback /> : this.props.children;
    }
}
