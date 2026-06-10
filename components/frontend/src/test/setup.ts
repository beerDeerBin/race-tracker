import '@testing-library/jest-dom/vitest';
// Components under test render translated strings — initialize i18n (en in jsdom).
import '../i18n';
import { registerUnauthorizedHandler } from '../utils/httpClient';
import { tokenStore } from '../utils/tokenStore';

// jsdom ships no ResizeObserver (used by the chart wrapper) — minimal no-op stand-in.
class ResizeObserverStub {
    observe(): void {}
    unobserve(): void {}
    disconnect(): void {}
}
globalThis.ResizeObserver ??= ResizeObserverStub as typeof ResizeObserver;

// jsdom has no object-URL support — the image components create/revoke them. Minimal stubs.
URL.createObjectURL ??= () => 'blob:stub';
URL.revokeObjectURL ??= () => {};

// Every test starts signed out with clean storage and no leaked 401 handler (the store
// and the handler are module-level singletons shared across test files).
beforeEach(() => {
    tokenStore.clear();
    sessionStorage.clear();
    registerUnauthorizedHandler(null);
});
