import { describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { usePlaybackClock } from './usePlaybackClock';
import type { PlaybackClockDeps } from './usePlaybackClock';

/** A controllable clock + rAF: tick() advances wall time and fires the pending frame. */
function fakeDeps() {
    let nowMs = 0;
    let pending: (() => void) | null = null;
    const deps: PlaybackClockDeps = {
        now: () => nowMs,
        requestFrame: (cb) => {
            pending = cb;
            return 1;
        },
        cancelFrame: () => {
            pending = null;
        },
    };
    return {
        deps,
        advance(ms: number) {
            nowMs += ms;
            const cb = pending;
            pending = null;
            cb?.();
        },
        hasFrame: () => pending !== null,
    };
}

describe('usePlaybackClock', () => {
    it('starts paused at time 0', () => {
        const { deps } = fakeDeps();
        const { result } = renderHook(() => usePlaybackClock(10, deps));

        expect(result.current.time).toBe(0);
        expect(result.current.playing).toBe(false);
    });

    it('advances by wall-clock delta × speed while playing', () => {
        const clock = fakeDeps();
        const { result } = renderHook(() => usePlaybackClock(10, clock.deps));

        act(() => result.current.play());
        act(() => clock.advance(1000)); // 1 s wall, speed 1 → +1 s
        expect(result.current.time).toBeCloseTo(1);

        act(() => result.current.setSpeed(2));
        act(() => clock.advance(1000)); // 1 s wall, speed 2 → +2 s
        expect(result.current.time).toBeCloseTo(3);
    });

    it('seek sets the time and clamps to [0, duration]', () => {
        const { deps } = fakeDeps();
        const { result } = renderHook(() => usePlaybackClock(10, deps));

        act(() => result.current.seek(4));
        expect(result.current.time).toBe(4);
        act(() => result.current.seek(99));
        expect(result.current.time).toBe(10);
        act(() => result.current.seek(-5));
        expect(result.current.time).toBe(0);
    });

    it('auto-pauses at the end', () => {
        const clock = fakeDeps();
        const { result } = renderHook(() => usePlaybackClock(2, clock.deps));

        act(() => result.current.play());
        act(() => clock.advance(5000)); // overshoots duration

        expect(result.current.time).toBe(2);
        expect(result.current.playing).toBe(false);
        expect(clock.hasFrame()).toBe(false);
    });

    it('pause stops advancing', () => {
        const clock = fakeDeps();
        const { result } = renderHook(() => usePlaybackClock(10, clock.deps));

        act(() => result.current.play());
        act(() => clock.advance(1000));
        act(() => result.current.pause());
        const frozen = result.current.time;
        act(() => clock.advance(1000));

        expect(result.current.time).toBe(frozen);
    });

    it('replaying from the end restarts at 0', () => {
        const clock = fakeDeps();
        const { result } = renderHook(() => usePlaybackClock(2, clock.deps));

        act(() => result.current.seek(2));
        act(() => result.current.play());

        expect(result.current.time).toBe(0);
        expect(result.current.playing).toBe(true);
    });
});
