import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PlaybackControls } from './PlaybackControls';
import type { PlaybackClock } from '../hooks/usePlaybackClock';

function makeClock(overrides: Partial<PlaybackClock> = {}): PlaybackClock {
    return {
        time: 0,
        playing: false,
        speed: 1,
        play: vi.fn(),
        pause: vi.fn(),
        toggle: vi.fn(),
        seek: vi.fn(),
        setSpeed: vi.fn(),
        ...overrides,
    };
}

describe('PlaybackControls', () => {
    it('toggles play/pause and shows the right label', async () => {
        const clock = makeClock();
        render(<PlaybackControls clock={clock} duration={10} />);

        expect(screen.getByRole('button', { name: 'Play' })).toBeInTheDocument();
        await userEvent.click(screen.getByRole('button', { name: 'Play' }));
        expect(clock.toggle).toHaveBeenCalled();
    });

    it('shows Pause while playing', () => {
        render(<PlaybackControls clock={makeClock({ playing: true })} duration={10} />);
        expect(screen.getByRole('button', { name: 'Pause' })).toBeInTheDocument();
    });

    it('seeks from the slider', () => {
        const clock = makeClock();
        render(<PlaybackControls clock={clock} duration={10} />);

        const slider = screen.getByRole('slider', { name: 'Playback position' });
        fireEvent.change(slider, { target: { value: '5' } });

        expect(clock.seek).toHaveBeenCalledWith(5);
    });

    it('changes speed', async () => {
        const clock = makeClock();
        render(<PlaybackControls clock={clock} duration={10} />);

        await userEvent.click(screen.getByRole('button', { name: '2×' }));
        expect(clock.setSpeed).toHaveBeenCalledWith(2);
    });
});
