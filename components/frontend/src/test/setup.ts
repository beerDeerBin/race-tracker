import '@testing-library/jest-dom/vitest';
// Components under test render translated strings — initialize i18n (en in jsdom).
import '../i18n';
import { registerUnauthorizedHandler } from '../utils/httpClient';
import { tokenStore } from '../utils/tokenStore';

// Every test starts signed out with clean storage and no leaked 401 handler (the store
// and the handler are module-level singletons shared across test files).
beforeEach(() => {
    tokenStore.clear();
    sessionStorage.clear();
    registerUnauthorizedHandler(null);
});
