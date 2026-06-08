import { afterEach, describe, expect, it } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LanguageSwitcher } from './LanguageSwitcher';
import i18n from '../i18n';

describe('LanguageSwitcher', () => {
    afterEach(async () => {
        localStorage.clear();
        await i18n.changeLanguage('en');
    });

    it('shows the current language and toggles to the next, persisting it', async () => {
        await act(() => i18n.changeLanguage('en'));
        render(<LanguageSwitcher />);

        // In the en locale the "next" label is the English word for German.
        expect(screen.getByRole('button', { name: /German/ })).toHaveTextContent('EN');

        await userEvent.click(screen.getByRole('button'));

        expect(i18n.language).toBe('de');
        expect(localStorage.getItem('race-tracker.lang')).toBe('de');
    });

    it('toggles back to English from German', async () => {
        await act(() => i18n.changeLanguage('de'));
        render(<LanguageSwitcher />);

        await userEvent.click(screen.getByRole('button'));

        expect(i18n.language).toBe('en');
    });
});
