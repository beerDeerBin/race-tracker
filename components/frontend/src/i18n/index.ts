import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './en.json';
import de from './de.json';

/**
 * i18n setup (/U40/): every user-visible string lives in the locale bundles. Language is
 * picked from the browser for now; the explicit switcher lands with story 7.8.
 */
void i18n.use(initReactI18next).init({
    resources: {
        en: { translation: en },
        de: { translation: de },
    },
    lng: typeof navigator !== 'undefined' && navigator.language.startsWith('de') ? 'de' : 'en',
    fallbackLng: 'en',
    interpolation: {
        // React already escapes rendered values.
        escapeValue: false,
    },
});

export default i18n;
