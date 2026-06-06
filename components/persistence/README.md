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

## Run & verify

- `tilt up` brings up `timescaledb` + `persistence`; `/health/ready`
  (http://localhost:8082/health/ready) turns healthy once RabbitMQ + Timescale are reachable.
- Inspect with psql: `SELECT * FROM timescaledb_information.hypertables;` and `\d runs`.
- Tests: `dotnet test RaceTracker.Persistence.sln` (the migration integration test needs Docker).

## Not here yet (later stories)

Consumer → validate → idempotent upsert and the `Run`/`Sample` domain + repository → **3.3**.
GraphQL read path → **4.1**. Roll-ups → **4.2**. Trajectory → **4.3**.
