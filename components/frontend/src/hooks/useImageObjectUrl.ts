import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { imageService } from '../services/imageService';

/**
 * Resolves a vehicle image to a browser object URL by fetching its bytes through the shared auth'd
 * HTTP instance (so the bearer token is sent, unlike a plain `<img src>`). The fetched blob is cached
 * by React Query (immutable per id); the object URL is created locally and **revoked on cleanup** to
 * avoid leaks. Returns `{ url, isPending, isError }`; `url` is null until the blob has loaded.
 */
export function useImageObjectUrl(deviceGuid: string, imageId: string) {
    const {
        data: blob,
        isPending,
        isError,
    } = useQuery({
        queryKey: ['image', deviceGuid, imageId],
        queryFn: () => imageService.getBlob(deviceGuid, imageId),
        staleTime: Infinity,
    });

    // Create the object URL inside the effect and revoke it on cleanup. This must NOT be a useMemo:
    // under StrictMode the memo value would be created once, then revoked by the first (discarded)
    // mount's cleanup, leaving the remount pointing at a revoked URL (a broken image). Re-creating it
    // per effect run keeps every mount backed by a live URL. setState-in-effect is the correct tool
    // here — the effect synchronizes React with an external resource (the object-URL lifecycle).
    const [url, setUrl] = useState<string | null>(null);

    /* eslint-disable react-hooks/set-state-in-effect */
    useEffect(() => {
        if (!blob) {
            setUrl(null);
            return;
        }
        const objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
        return () => URL.revokeObjectURL(objectUrl);
    }, [blob]);
    /* eslint-enable react-hooks/set-state-in-effect */

    return { url, isPending, isError };
}
