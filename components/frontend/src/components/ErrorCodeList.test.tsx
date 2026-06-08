import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ErrorCodeList } from './ErrorCodeList';

describe('ErrorCodeList', () => {
    it('renders nothing when no error is asserted', () => {
        const { container } = render(<ErrorCodeList errorCode={0} />);
        expect(container).toBeEmptyDOMElement();
    });

    it('renders the battery-critical label for bit 42', () => {
        render(<ErrorCodeList errorCode={2 ** 42} />);
        expect(screen.getByText('Battery critical')).toBeInTheDocument();
    });

    it('renders a translated chip per set bit', () => {
        // bits 9 (Wi-Fi connect) + 34 (IMU read)
        render(<ErrorCodeList errorCode={2 ** 9 + 2 ** 34} />);
        expect(screen.getByText('Wi-Fi connect error')).toBeInTheDocument();
        expect(screen.getByText('IMU read error')).toBeInTheDocument();
    });
});
