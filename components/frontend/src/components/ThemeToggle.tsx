import { useTranslation } from 'react-i18next';
import { useTheme } from '../hooks/useTheme';
import { THEMES } from '../utils/themeStore';

const GLYPHS = { light: '☀', dark: '☾', system: '◐' } as const;

/** Cycles the color scheme light → dark → system; shows the active preference. */
export function ThemeToggle() {
    const { t } = useTranslation();
    const { theme, setTheme } = useTheme();
    const next = THEMES[(THEMES.indexOf(theme) + 1) % THEMES.length]!;

    return (
        <button
            type="button"
            onClick={() => setTheme(next)}
            title={t('theme.switchTo', { mode: t(`theme.${next}`) })}
            aria-label={t('theme.switchTo', { mode: t(`theme.${next}`) })}
            className="flex items-center gap-1.5 rounded border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-200 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
        >
            <span aria-hidden="true">{GLYPHS[theme]}</span>
            {t(`theme.${theme}`)}
        </button>
    );
}
