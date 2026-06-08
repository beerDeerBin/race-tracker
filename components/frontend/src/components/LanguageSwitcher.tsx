import { useTranslation } from 'react-i18next';
import { Languages } from 'lucide-react';
import { langStore, LANGUAGES } from '../utils/langStore';
import type { Language } from '../utils/langStore';

/** Cycles the UI language (en⇄de), persisting the choice (/U40/). */
export function LanguageSwitcher() {
    const { i18n, t } = useTranslation();
    const current = (LANGUAGES as readonly string[]).includes(i18n.language)
        ? (i18n.language as Language)
        : 'en';
    const next = LANGUAGES[(LANGUAGES.indexOf(current) + 1) % LANGUAGES.length]!;

    const switchTo = () => {
        langStore.set(next);
        void i18n.changeLanguage(next);
    };

    return (
        <button
            type="button"
            onClick={switchTo}
            title={t('language.switchTo', { lang: t(`language.${next}`) })}
            aria-label={t('language.switchTo', { lang: t(`language.${next}`) })}
            className="inline-flex items-center gap-1.5 rounded border border-slate-300 px-3 py-1.5 text-sm text-slate-700 transition-colors hover:border-f1-red hover:text-f1-red dark:border-slate-700 dark:text-slate-300"
        >
            <Languages className="h-4 w-4" aria-hidden="true" />
            {current.toUpperCase()}
        </button>
    );
}
