/**
 * Bold racing-themed page backdrop (/U20/): a stylized circuit loop with a dashed racing line,
 * red speed streaks and a checkered-flag corner. Fixed behind all content
 * (`pointer-events-none`, `-z-10`); purely decorative, so it's hidden from assistive tech. The
 * opaque panels (.card / header / footer / dialogs) sit on top, keeping dense content legible.
 */
export function RacingBackground() {
    return (
        <div aria-hidden="true" className="pointer-events-none fixed inset-0 -z-10 overflow-hidden">
            <svg
                className="h-full w-full"
                viewBox="0 0 1440 1024"
                preserveAspectRatio="xMidYMid slice"
                fill="none"
            >
                <defs>
                    <pattern id="rt-checker" width="44" height="44" patternUnits="userSpaceOnUse">
                        <rect width="22" height="22" fill="currentColor" />
                        <rect x="22" y="22" width="22" height="22" fill="currentColor" />
                    </pattern>
                    <linearGradient id="rt-streak" x1="0" y1="0" x2="1" y2="0">
                        <stop offset="0%" stopColor="#e10600" stopOpacity="0" />
                        <stop offset="100%" stopColor="#e10600" stopOpacity="0.55" />
                    </linearGradient>
                </defs>

                {/* Checkered-flag block (right side, dropped below the header so it stays visible). */}
                <g className="text-slate-300 dark:text-slate-800" opacity="0.55">
                    <rect
                        x="1170"
                        y="220"
                        width="360"
                        height="200"
                        fill="url(#rt-checker)"
                        transform="rotate(9 1350 320)"
                    />
                </g>

                {/* Stylized circuit loop — thick red asphalt + dashed racing line. */}
                <path
                    d="M 230 770 C 70 700 120 450 330 450 C 540 450 560 610 770 610 C 1000 610 1000 350 830 300 C 690 258 700 130 930 140 C 1210 152 1340 320 1250 540 C 1180 710 1330 770 1170 880 C 980 1010 520 960 360 895 C 250 850 320 812 230 770 Z"
                    stroke="#e10600"
                    strokeOpacity="0.16"
                    strokeWidth="34"
                    strokeLinejoin="round"
                />
                <path
                    d="M 230 770 C 70 700 120 450 330 450 C 540 450 560 610 770 610 C 1000 610 1000 350 830 300 C 690 258 700 130 930 140 C 1210 152 1340 320 1250 540 C 1180 710 1330 770 1170 880 C 980 1010 520 960 360 895 C 250 850 320 812 230 770 Z"
                    className="text-slate-400 dark:text-slate-600"
                    stroke="currentColor"
                    strokeOpacity="0.5"
                    strokeWidth="2"
                    strokeDasharray="14 18"
                    strokeLinejoin="round"
                />

                {/* Speed streaks. */}
                <g strokeLinecap="round">
                    <line
                        x1="-60"
                        y1="150"
                        x2="430"
                        y2="150"
                        stroke="url(#rt-streak)"
                        strokeWidth="7"
                    />
                    <line
                        x1="-60"
                        y1="185"
                        x2="300"
                        y2="185"
                        stroke="url(#rt-streak)"
                        strokeWidth="5"
                    />
                    <line
                        x1="1010"
                        y1="940"
                        x2="1500"
                        y2="940"
                        stroke="url(#rt-streak)"
                        strokeWidth="7"
                    />
                    <line
                        x1="1140"
                        y1="975"
                        x2="1500"
                        y2="975"
                        stroke="url(#rt-streak)"
                        strokeWidth="5"
                    />
                </g>
            </svg>
        </div>
    );
}
