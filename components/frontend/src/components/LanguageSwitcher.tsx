import { useTranslation } from 'react-i18next';
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
            className="rounded border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-200 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
        >
            {current.toUpperCase()}
        </button>
    );
}
