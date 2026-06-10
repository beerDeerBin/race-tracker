import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren, ReactElement } from 'react';
import { ImagePreviewDialog } from './ImagePreviewDialog';
import { imageService } from '../services/imageService';

vi.mock('../services/imageService', () => ({
    imageService: { getBlob: vi.fn() },
}));

const getBlobMock = vi.mocked(imageService.getBlob);

function render_(ui: ReactElement) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    function Providers({ children }: PropsWithChildren) {
        return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
    }
    return render(ui, { wrapper: Providers });
}

function renderDialog(onClose = vi.fn()) {
    getBlobMock.mockResolvedValue(new Blob([new Uint8Array([1])], { type: 'image/png' }));
    render_(
        <ImagePreviewDialog deviceGuid="GUID-Aa" imageId="img-1" alt="kart" onClose={onClose} />,
    );
    return onClose;
}

describe('ImagePreviewDialog', () => {
    beforeEach(() => getBlobMock.mockReset());

    it('renders a modal dialog and focuses the close button', async () => {
        renderDialog();

        expect(screen.getByRole('dialog')).toHaveAttribute('aria-modal', 'true');
        await waitFor(() =>
            expect(screen.getByRole('button', { name: 'Close preview' })).toHaveFocus(),
        );
    });

    it('closes on Escape', async () => {
        const onClose = renderDialog();

        await userEvent.keyboard('{Escape}');

        expect(onClose).toHaveBeenCalled();
    });

    it('closes on the close button', async () => {
        const onClose = renderDialog();

        await userEvent.click(screen.getByRole('button', { name: 'Close preview' }));

        expect(onClose).toHaveBeenCalled();
    });

    it('closes when the backdrop is clicked', async () => {
        const onClose = renderDialog();

        // The dialog stops propagation; clicking outside it (the backdrop) closes.
        await userEvent.click(screen.getByRole('presentation'));

        expect(onClose).toHaveBeenCalled();
    });
});
