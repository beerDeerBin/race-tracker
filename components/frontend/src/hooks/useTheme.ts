import { useContext } from 'react';
import { ThemeContext } from '../context/ThemeContext';
import type { ThemeContextValue } from '../context/ThemeContext';

/** Hooks-layer access to the theme context; components never touch the context directly. */
export function useTheme(): ThemeContextValue {
    const context = useContext(ThemeContext);
    if (!context) {
        throw new Error('useTheme must be used within a ThemeProvider');
    }
    return context;
}
