# TP-PERS — Persistence service

Time-series persistence for race-tracker (M3–M4). Consumes normalised telemetry from
RabbitMQ and stores it in **TimescaleDB** (write path, M3), and serves it via GraphQL
(read path, M4). This README covers the **3.1 + 3.2** foundation: the database, the
schema/migrations, and the service grundgerüst.

## Layout

Four-layer Clean/Onion service (see [doc/CONVENTIONS.md](../../doc/CONVENTIONS.md)):

```
src/RaceTracker.Persistence.Domain          # plain models (Run/Sample land in 3.3)
src/RaceTracker.Persistence.Application      # ports (IXxx), Options, use cases
src/RaceTracker.Persistence.Infrastructure   # adapters: Npgsql migrator + connectivity probes, SQL
src/RaceTracker.Persistence.Api              # composition root: DI, health, migration startup
```

Observability (Serilog, correlation-id, split `/health/live` + `/health/ready`) is reused
from `RaceTracker.BuildingBlocks` — referenced, not copied.

## Database & schema (story 3.1)

TimescaleDB runs in Tilt (`docker-compose.yml`, image `timescale/timescaledb:2.17.2-pg16`,
db `racetracker`). The schema is **service-owned**: ordered, idempotent SQL scripts in
[`Infrastructure/Migrations/Sql`](src/RaceTracker.Persistence.Infrastructure/Migrations/Sql)
are applied at startup by `NpgsqlDatabaseMigrator` (behind the `IDatabaseMigrator` port) and
tracked in a `schema_migrations` table, so re-running is a no-op. The same scripts back the
integration test.

| Object | Kind | Key columns |
|---|---|---|
| `samples` | **Hypertable** (partitioned on `sample_index`) | `device_guid`, `run_id`, `sample_index`, `ax, ay, az, gx, gy, gz` |
| `runs` | Plain table | `device_guid`, `run_id`, `num_samples`, `odr_hz`, `accel_range`, `gyro_range`, `started_at`, `ended_at`, `received_samples` |

### Time-base convention (`/F54/`)

A sample carries **no timestamp**. Each sample's time is derived from the run's output data
rate (ODR):

```
t = sample_index / odr_hz
```

`sample_index` is the absolute index within a run (0-based). The `samples` hypertable is
therefore partitioned on the **integer `sample_index`** dimension (no wall-clock column).
Telemetry is keyed by `device_guid + run_id + sample_index` — the GUID is the cross-service
correlation key, so there are **no foreign keys to other services' stores**.

## Trajectory / Dead Reckoning (story 4.3)

Per run we derive a **2D ground track** ("Fahrstrecke") from the stored samples so the
frontend (7.9) can draw a map / play the run back **without computing anything client-side**.
It is computed behind the swappable `ITrajectoryCalculator` port (adapter:
`DeadReckoningTrajectoryCalculator`) and persisted into a derived `trajectory_points` table —
the raw `samples` are **never** modified.

| Object | Kind | Key columns |
|---|---|---|
| `trajectory_points` | Plain derived table | `device_guid`, `run_id`, `sample_index`, `t_seconds`, `x`, `y`, `heading` |

**Algorithm.** With step `dt = 1 / odr_hz`: heading is the yaw rate `gz` integrated over time;
the in-plane acceleration (`ax`, `ay`) is rotated into world coordinates by the heading and
**double-integrated** (→ velocity → position). The vertical axis `az` (gravity) is ignored — this
is a ground track. The first sample anchors the path at the **origin** (`(0,0)`, heading 0), and
`t = sample_index / odr_hz`. Each point depends only on the samples up to it, so the result is
**deterministic** and stable as more samples arrive.

**Eager build, pure read.** A background `TrajectoryProjectionWorker` periodically rebuilds runs
whose trajectory is stale (point count ≠ `received_samples`) once they have settled (no new batch
for a short window) — so the end-of-run batch burst coalesces into a single deterministic recompute
(`DELETE` + re-`INSERT`, idempotent). The GraphQL `trajectory(runId)` query is therefore a **pure
read** of the derived table (optionally scoped/`stride`-downsampled). ODR is taken from the run, or
falls back to **104 Hz** (PROTOCOL default) until 5.x plumbs the real value.

> **Limitation.** Pure IMU dead reckoning **drifts**: double-integrating noisy acceleration
> accumulates error without bound. The path is a plausible **approximation, not GPS**. A
> drift-corrected / zero-velocity-update algorithm can replace the adapter behind the port with no
> contract or schema change.

## Run & verify

- `tilt up` brings up `timescaledb` + `persistence`; `/health/ready`
  (http://localhost:8082/health/ready) turns healthy once RabbitMQ + Timescale are reachable.
- Inspect with psql: `SELECT * FROM timescaledb_information.hypertables;` and `\d runs`.
- Tests: `dotnet test RaceTracker.Persistence.sln` (the migration integration test needs Docker).

## Not here yet (later stories)

Consumer → validate → idempotent upsert and the `Run`/`Sample` domain + repository → **3.3**.
GraphQL read path → **4.1**. Roll-ups → **4.2**. Trajectory → **4.3** (above).

## GraphQL auth (since story 7.5)

`/graphql` validates the management-issued JWT (secure-by-default, /F12/). The Banana
Cake Pop IDE therefore also needs a token: `POST /login` on the management service and
set the `Authorization: Bearer <accessToken>` header in the IDE's connection settings.
The signing key/issuer/audience come from the shared `Auth:Jwt` section (building-blocks
`JwtValidationOptions`); override `Auth__Jwt__SigningKey` in real deployments.
