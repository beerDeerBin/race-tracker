import { useTranslation } from 'react-i18next';
import { Monitor, Moon, Sun } from 'lucide-react';
import type { ComponentType } from 'react';
import { useTheme } from '../hooks/useTheme';
import { THEMES } from '../utils/themeStore';
import type { Theme } from '../utils/themeStore';

const ICONS: Record<Theme, ComponentType<{ className?: string }>> = {
    light: Sun,
    dark: Moon,
    system: Monitor,
};

/** Cycles the color scheme light → dark → system; shows the active preference. */
export function ThemeToggle() {
    const { t } = useTranslation();
    const { theme, setTheme } = useTheme();
    const next = THEMES[(THEMES.indexOf(theme) + 1) % THEMES.length]!;
    const Icon = ICONS[theme];

    return (
        <button
            type="button"
            onClick={() => setTheme(next)}
            title={t('theme.switchTo', { mode: t(`theme.${next}`) })}
            aria-label={t('theme.switchTo', { mode: t(`theme.${next}`) })}
            className="inline-flex items-center gap-1.5 rounded border border-slate-300 px-3 py-1.5 text-sm text-slate-700 transition-colors hover:border-f1-red hover:text-f1-red dark:border-slate-700 dark:text-slate-300"
        >
            <Icon className="h-4 w-4" aria-hidden="true" />
            {t(`theme.${theme}`)}
        </button>
    );
}
