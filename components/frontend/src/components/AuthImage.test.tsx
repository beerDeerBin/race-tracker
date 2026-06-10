import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren, ReactElement } from 'react';
import { AuthImage } from './AuthImage';
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

describe('AuthImage', () => {
    beforeEach(() => getBlobMock.mockReset());

    it('renders the image once the authenticated blob loads', async () => {
        getBlobMock.mockResolvedValueOnce(new Blob([new Uint8Array([1])], { type: 'image/png' }));

        render_(<AuthImage deviceGuid="GUID-Aa" imageId="img-1" alt="kart photo" />);

        const img = await screen.findByAltText('kart photo');
        expect(img.getAttribute('src')).toMatch(/^blob:/);
        expect(getBlobMock).toHaveBeenCalledWith('GUID-Aa', 'img-1');
    });

    it('shows a fallback when the image fails to load', async () => {
        getBlobMock.mockRejectedValueOnce(new Error('boom'));

        render_(<AuthImage deviceGuid="GUID-Aa" imageId="img-1" alt="kart photo" />);

        expect(
            await screen.findByRole('img', { name: 'Image could not be loaded' }),
        ).toBeInTheDocument();
    });
});
