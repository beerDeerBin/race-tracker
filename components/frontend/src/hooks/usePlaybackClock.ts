import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * A playback clock for the trajectory animation (/U20/): advances `time` (seconds) by
 * wall-clock delta × speed via requestAnimationFrame — never by a fixed per-frame step,
 * so dropped frames don't distort timing. Clamps to [0, duration] and auto-pauses at the
 * end. The clock + rAF are injectable for deterministic tests.
 */

export const PLAYBACK_SPEEDS = [1, 2, 4] as const;
export type PlaybackSpeed = (typeof PLAYBACK_SPEEDS)[number];

export interface PlaybackClock {
    time: number;
    playing: boolean;
    speed: PlaybackSpeed;
    play(): void;
    pause(): void;
    toggle(): void;
    seek(time: number): void;
    setSpeed(speed: PlaybackSpeed): void;
}

export interface PlaybackClockDeps {
    now: () => number;
    requestFrame: (callback: () => void) => number;
    cancelFrame: (handle: number) => void;
}

// Module-level so the default is referentially stable: the rAF effect depends on `deps`,
// so a caller passing an inline object literal would resubscribe the loop every render.
// Pass a memoized object if you ever override this.
const defaultDeps: PlaybackClockDeps = {
    now: () => performance.now(),
    requestFrame: (callback) => requestAnimationFrame(callback),
    cancelFrame: (handle) => cancelAnimationFrame(handle),
};

export function usePlaybackClock(
    duration: number,
    deps: PlaybackClockDeps = defaultDeps,
): PlaybackClock {
    const [time, setTime] = useState(0);
    const [playing, setPlaying] = useState(false);
    const [speed, setSpeedState] = useState<PlaybackSpeed>(1);

    // Mutable refs so the rAF loop reads the latest values without re-subscribing.
    const timeRef = useRef(0);
    const speedRef = useRef<PlaybackSpeed>(1);
    const durationRef = useRef(duration);
    useEffect(() => {
        durationRef.current = duration;
    }, [duration]);

    const apply = useCallback((next: number) => {
        const clamped = Math.min(Math.max(next, 0), durationRef.current);
        timeRef.current = clamped;
        setTime(clamped);
        return clamped;
    }, []);

    const play = useCallback(() => {
        // Replaying from the end restarts from the beginning.
        if (timeRef.current >= durationRef.current) {
            apply(0);
        }
        setPlaying(true);
    }, [apply]);
    const pause = useCallback(() => setPlaying(false), []);
    const toggle = useCallback(() => setPlaying((p) => !p), []);
    const seek = useCallback((next: number) => apply(next), [apply]);
    const setSpeed = useCallback((next: PlaybackSpeed) => {
        speedRef.current = next;
        setSpeedState(next);
    }, []);

    useEffect(() => {
        if (!playing) {
            return;
        }
        let frame = 0;
        let last = deps.now();
        const tick = () => {
            const current = deps.now();
            const deltaSeconds = ((current - last) / 1000) * speedRef.current;
            last = current;
            const next = apply(timeRef.current + deltaSeconds);
            if (next >= durationRef.current) {
                setPlaying(false); // auto-pause at the end
                return;
            }
            frame = deps.requestFrame(tick);
        };
        frame = deps.requestFrame(tick);
        return () => deps.cancelFrame(frame);
    }, [playing, apply, deps]);

    return { time, playing, speed, play, pause, toggle, seek, setSpeed };
}
