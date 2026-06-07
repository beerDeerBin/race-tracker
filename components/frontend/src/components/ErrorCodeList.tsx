import { useTranslation } from 'react-i18next';
import { decodeErrorBits } from '../utils/errorBitmask';

/**
 * Plaintext error chips (/U30/): decodes the device error bitmask (PROTOCOL §5.1) to
 * named, translated labels. Renders nothing when no error is asserted.
 */
export function ErrorCodeList({ errorCode }: { errorCode: number }) {
    const { t } = useTranslation();
    const keys = decodeErrorBits(errorCode);

    if (keys.length === 0) {
        return null;
    }

    return (
        <div className="mt-1 flex flex-wrap gap-1">
            {keys.map((key) => (
                <span
                    key={key}
                    className="inline-block rounded bg-red-100 px-1.5 py-0.5 text-xs font-medium text-red-700 dark:bg-red-900 dark:text-red-300"
                >
                    {t(key)}
                </span>
            ))}
        </div>
    );
}
