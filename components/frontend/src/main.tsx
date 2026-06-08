import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import './i18n';
import './index.css';
import App from './App.tsx';
import { ErrorBoundary } from './components/ErrorBoundary.tsx';
import { AuthProvider } from './context/AuthProvider.tsx';
import { ThemeProvider } from './context/ThemeProvider.tsx';

const queryClient = new QueryClient();

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <ThemeProvider>
            <ErrorBoundary>
                <BrowserRouter>
                    <AuthProvider>
                        <QueryClientProvider client={queryClient}>
                            <App />
                        </QueryClientProvider>
                    </AuthProvider>
                </BrowserRouter>
            </ErrorBoundary>
        </ThemeProvider>
    </StrictMode>,
);
