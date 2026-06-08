# race-tracker-frontend (TP-FE)

React + TypeScript SPA against the **real** backend endpoints (anti-mock): management
REST (`:8083`), persistence GraphQL (`:8082`, from story 7.5), realtime SignalR
(`:8084`, from story 7.2).

## Prerequisites

- Node ≥ 20.19 (22 LTS recommended, see `.nvmrc`)
- The backend stack: `cd components/ && tilt up` (the `frontend` Tilt resource runs this
  dev server automatically)

## Commands

```sh
npm install        # once / after package.json changes
npm run dev        # Vite dev server at http://localhost:5173
npm run test       # unit tests (Vitest + React Testing Library)
npm run lint       # ESLint
npm run typecheck  # tsc -b
npm run format     # Prettier
npm run build      # production build (dist/) — deployment container is a post-M7 follow-up
```

## Configuration

Backend origins come from `VITE_MANAGEMENT_URL` / `VITE_PERSISTENCE_URL` /
`VITE_REALTIME_URL` (see `.env.development`, override via `.env.local`), read in exactly
one place: `src/utils/config.ts`.

Dev login: the management seed user (`admin` / `change-me` unless overridden).

## Architecture (binding — ARCHITECTURE_PRINCIPLES.md §8, PFLICHTENHEFT §9)

```
pages → components → hooks → services → utils
models/   typed mirrors of the API contracts
context/  auth provider (route guards consume it via useAuth)
i18n/     externalized strings, en + de
```

- **One** HTTP instance (`src/utils/httpClient.ts`) with interceptors: Bearer-token
  injection + central `401` → logout. Components never call the network directly.
- Session lives in `src/utils/tokenStore.ts` (in-memory + sessionStorage mirror) — the
  only storage reader/writer.
- Color scheme (light / dark / system) via `ThemeProvider` + Tailwind's class-based
  `dark:` variant; persisted in localStorage, flash-free via an inline script in
  `index.html` (storage key kept in sync with `src/utils/themeStore.ts`).
- Device `guid`s are opaque, **case-sensitive** strings — never lowercase or re-parse.
