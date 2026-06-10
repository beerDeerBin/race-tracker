import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ConfirmDialog } from './ConfirmDialog';

function renderDialog(props?: Partial<Parameters<typeof ConfirmDialog>[0]>) {
    const onConfirm = vi.fn();
    const onClose = vi.fn();
    render(
        <ConfirmDialog
            title="Delete image"
            message="Are you sure?"
            confirmLabel="Delete"
            onConfirm={onConfirm}
            onClose={onClose}
            {...props}
        />,
    );
    return { onConfirm, onClose };
}

describe('ConfirmDialog', () => {
    it('focuses the confirm button and fires onConfirm', async () => {
        const { onConfirm } = renderDialog();

        const confirm = screen.getByRole('button', { name: 'Delete' });
        await waitFor(() => expect(confirm).toHaveFocus());
        await userEvent.click(confirm);

        expect(onConfirm).toHaveBeenCalled();
    });

    it('cancels via the cancel button and Escape', async () => {
        const { onClose } = renderDialog();

        await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
        await userEvent.keyboard('{Escape}');

        expect(onClose).toHaveBeenCalledTimes(2);
    });

    it('disables the confirm button while busy', () => {
        renderDialog({ busy: true });

        expect(screen.getByRole('button', { name: 'Delete' })).toBeDisabled();
    });
});
