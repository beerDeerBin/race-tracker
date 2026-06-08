import type { TrajectoryPoint } from '../models/graphql';

/**
 * Pure geometry for the trajectory map (/F80/, /D50/). Coordinates are local meters with
 * +y up (math convention); SVG y points down, so the render space flips y (`svgY = -y`).
 * All functions are side-effect-free and unit-tested — no rendering, no physics.
 */

export interface Bounds {
    minX: number;
    minY: number;
    maxX: number;
    maxY: number;
}

/** Axis-aligned bounds of the path; a zero-size box (around the point/origin) when degenerate. */
export function computeBounds(points: readonly TrajectoryPoint[]): Bounds {
    if (points.length === 0) {
        return { minX: 0, minY: 0, maxX: 0, maxY: 0 };
    }
    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    for (const { x, y } of points) {
        if (x < minX) minX = x;
        if (y < minY) minY = y;
        if (x > maxX) maxX = x;
        if (y > maxY) maxY = y;
    }
    return { minX, minY, maxX, maxY };
}

/**
 * SVG viewBox fitting the bounds with `padding` (meters) on all sides, expressed in the
 * y-flipped render space (so `svgY = -y`). A degenerate axis is widened to 1 m so a
 * straight-line or single-point path still has a visible box.
 */
export function boundsToViewBox(bounds: Bounds, padding = 1): string {
    const spanX = Math.max(bounds.maxX - bounds.minX, 1);
    const spanY = Math.max(bounds.maxY - bounds.minY, 1);
    const x = bounds.minX - padding;
    // Top edge in flipped space corresponds to the largest y.
    const yTop = -bounds.maxY - padding;
    const width = spanX + padding * 2;
    const height = spanY + padding * 2;
    return `${x} ${yTop} ${width} ${height}`;
}

/** SVG `<path>` d in the y-flipped render space. */
export function pathD(points: readonly TrajectoryPoint[]): string {
    if (points.length === 0) {
        return '';
    }
    return points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${-p.y}`).join(' ');
}

/**
 * Marker rotation in SVG degrees for a heading in radians. Heading is CCW from +x in math
 * space; the y-flip negates the visual angle, so `deg = -heading * 180/π`. A marker drawn
 * pointing along +x (East) then points along the travel direction.
 */
export function headingToSvgDegrees(headingRad: number): number {
    return (-headingRad * 180) / Math.PI;
}

/**
 * SVG-degrees orientation of the path **tangent** at `index` — the direction of travel along the
 * drawn track (central difference of the neighbouring points, in the y-flipped render space). The
 * vehicle marker uses this so it follows the track, rather than the raw device <c>heading</c>
 * (integrated yaw), which drifts away from the drawn path under IMU dead-reckoning. Returns 0 for a
 * degenerate span (&lt; 2 points, or coincident neighbours).
 */
export function tangentSvgDegrees(points: readonly TrajectoryPoint[], index: number): number {
    if (points.length < 2) {
        return 0;
    }
    const i = Math.min(Math.max(index, 0), points.length - 1);
    const a = points[Math.max(0, i - 1)]!;
    const b = points[Math.min(points.length - 1, i + 1)]!;
    const dx = b.x - a.x;
    const dy = a.y - b.y; // y-flipped render space: svgY = -y
    if (dx === 0 && dy === 0) {
        return 0;
    }
    return (Math.atan2(dy, dx) * 180) / Math.PI;
}

/** Total run time in seconds (the last point's t), 0 for an empty path. */
export function totalDuration(points: readonly TrajectoryPoint[]): number {
    return points.length > 0 ? points[points.length - 1]!.t : 0;
}

export interface TrajectoryStats {
    distanceM: number;
    topSpeedMps: number;
    avgSpeedMps: number;
    peakAccelMps2: number;
    durationS: number;
}

/**
 * Summary telemetry derived from the path (display-only "fun stats"): total distance, top/avg speed,
 * peak acceleration and duration. Per-segment speed is displacement over Δt; peak accel is the
 * largest change in those speeds over Δt. Approximate — the path is dead-reckoned (it drifts), so
 * these are indicative, not lab-grade.
 */
export function trajectoryStats(points: readonly TrajectoryPoint[]): TrajectoryStats {
    const durationS = totalDuration(points);
    if (points.length < 2) {
        return { distanceM: 0, topSpeedMps: 0, avgSpeedMps: 0, peakAccelMps2: 0, durationS };
    }

    let distanceM = 0;
    let topSpeedMps = 0;
    let peakAccelMps2 = 0;
    let prevSpeed = 0;
    for (let i = 1; i < points.length; i++) {
        const a = points[i - 1]!;
        const b = points[i]!;
        const seg = Math.hypot(b.x - a.x, b.y - a.y);
        distanceM += seg;
        const dt = b.t - a.t;
        if (dt > 0) {
            const speed = seg / dt;
            topSpeedMps = Math.max(topSpeedMps, speed);
            peakAccelMps2 = Math.max(peakAccelMps2, Math.abs(speed - prevSpeed) / dt);
            prevSpeed = speed;
        }
    }

    const avgSpeedMps = durationS > 0 ? distanceM / durationS : 0;
    return { distanceM, topSpeedMps, avgSpeedMps, peakAccelMps2, durationS };
}

/**
 * The point active at playback time `tSeconds`: the last point with `t <= tSeconds`,
 * clamped to the ends. Null only for an empty path. Linear scan is fine at the point
 * counts here; callers playing back can pass a hint later if needed.
 */
export function pointAtTime(
    points: readonly TrajectoryPoint[],
    tSeconds: number,
): TrajectoryPoint | null {
    if (points.length === 0) {
        return null;
    }
    if (tSeconds <= points[0]!.t) {
        return points[0]!;
    }
    const last = points[points.length - 1]!;
    if (tSeconds >= last.t) {
        return last;
    }
    let active = points[0]!;
    for (const point of points) {
        if (point.t <= tSeconds) {
            active = point;
        } else {
            break;
        }
    }
    return active;
}
