import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './en.json';
import de from './de.json';
import { initialLanguage } from '../utils/langStore';

/**
 * i18n setup (/U40/): every user-visible string lives in the locale bundles. The initial
 * language is the persisted preference, else the browser, else English; the explicit
 * switcher (story 7.8) changes it at runtime.
 */
void i18n.use(initReactI18next).init({
    resources: {
        en: { translation: en },
        de: { translation: de },
    },
    lng: initialLanguage(),
    fallbackLng: 'en',
    interpolation: {
        // React already escapes rendered values.
        escapeValue: false,
    },
});

export default i18n;
