# Story 4.3 — Trajectory (Dead Reckoning) compute + persist + GraphQL read

## Context

M3/M4 already store raw IMU samples (`samples` hypertable) and run metadata (`runs`)
in TimescaleDB and expose a CQRS read path over GraphQL (`runs`, `samples`,
`runRollup`). Story 4.3 adds a **derived 2D ground track** ("Fahrstrecke") per run:
from the stored samples we integrate a dead-reckoning path so the frontend (7.9) can
draw a map / play the run back **without computing anything client-side**.

Decision (with the user): **eager build on the write side via a background projection
worker** — not lazy-on-read. The worker detects runs whose trajectory is stale using
the existing `runs.received_samples` + `runs.updated_at` (both maintained by the 3.3
upsert), waits for the end-of-run batch burst to settle, and does one deterministic
full recompute per run. The GraphQL query is a **pure read**. ODR is taken from the run
or falls back to 104 Hz (PROTOCOL default) while `runs.odr_hz` is NULL.

## Acceptance criteria

- [x] 2D ground track: heading from `gz` (integrated); `ax/ay` rotated to world coords
      and double-integrated to position; start fixed at `(0,0)`, heading 0;
      `t = index / odr_hz`.
- [x] Calculation behind `ITrajectoryCalculator` port (Application), adapter
      `DeadReckoningTrajectoryCalculator` (Infrastructure) → swappable without
      contract/schema change.
- [x] Precomputed + persisted in derived `trajectory_points` (raw `samples` untouched),
      idempotently regenerable per `guid/runId`.
- [x] GraphQL pure-read query returns the ordered `{ index, t, x, y, heading }` sequence
      (optional `stride` downsampling).
- [x] Real run: expected point count, first point `(0,0)`, deterministic.
- [x] Drift limitation documented (calculator XML + README).

## What was built

- **Domain:** `TrajectoryPoint`, `RunTrajectory`.
- **Application:** `ITrajectoryCalculator`, `ITrajectoryRepository`,
  `ITelemetryReadStore.GetTrajectoryAsync`, `PendingRun`, `TrajectoryQuery`
  (limit clamp + `stride`), `TrajectoryProjectionService` (catch-log-continue),
  `TrajectoryProjectionWorker` (`PeriodicTimer`, non-overlapping), `TrajectoryOptions`
  (DefaultOdrHz=104, RebuildIntervalSeconds=3, SettleSeconds=2), 2 metrics, DI.
- **Infrastructure:** `DeadReckoningTrajectoryCalculator` (forward-Euler, prefix-stable),
  `NpgsqlTrajectoryRepository` (stale-run query, full-sample load, DELETE+chunked INSERT),
  `NpgsqlTelemetryReadStore.GetTrajectoryAsync`, `V006__create_trajectories.sql`, DI.
- **Api:** `TrajectoryPointDto` + `trajectory(...)` resolver (pure read).
- **Docs:** README "Trajectory / Dead Reckoning" section incl. drift limitation.
- **Tests:** `DeadReckoningTrajectoryCalculatorTests`, `TrajectoryQueryTests`,
  `TrajectoryProjectionServiceTests` (unit); `TrajectoryReadIntegrationTests`
  (real Timescale); updated `MigrationsIntegrationTests` (6 migrations + derived table).

## Verification result

Run from `components/persistence/` (2026-06-06):

- `dotnet format RaceTracker.Persistence.sln --verify-no-changes` → **clean** (no changes).
- `dotnet build RaceTracker.Persistence.sln -c Debug` → **0 Warning(s), 0 Error(s)**.
- `dotnet test` UnitTests → **Passed: 54, Failed: 0**.
- `dotnet test` IntegrationTests (Docker, real TimescaleDB) → **Passed: 6, Failed: 0**.

The integration test confirms: the `trajectory` query is empty before the projection
runs (pure read, not lazy), then returns exactly the seeded point count with the first
point at `(0,0,0,0)`, is deterministic across re-runs (second rebuild finds nothing
stale), supports `stride` downsampling, and leaves the raw `samples` table unchanged.

## Out of scope (deferred)

- Frontend map + playback (7.9). Real ODR/run-lifecycle plumbing (5.x) — default 104 Hz
  until then. Drift correction / ZUPT (future swap behind the port). 3D/vertical motion.
