# TP-MGMT — Management service

System of record for race-tracker (M5): identity (single-user auth), vehicles/registry,
device discovery and **command dispatch** to the device. Backed by **MongoDB**. This README
covers the **5.1** foundation: the database and the service grundgerüst.

## Layout

Four-layer Clean/Onion service (see [doc/CONVENTIONS.md](../../doc/CONVENTIONS.md)):

```
src/RaceTracker.Management.Domain          # plain models (User/Vehicle land in 5.2/5.3)
src/RaceTracker.Management.Application      # ports (IXxx), Options, use cases
src/RaceTracker.Management.Infrastructure   # adapters: Mongo client + connectivity probe
src/RaceTracker.Management.Api              # composition root: DI, health, middleware
```

Observability (Serilog, correlation-id, split `/health/live` + `/health/ready`, `/metrics`) is
reused from `RaceTracker.BuildingBlocks` — referenced, not copied.

## Database (story 5.1)

MongoDB runs in Tilt (`docker-compose.yml`, image `mongo:8.0`, db `racetracker`). The dev
container runs **without auth** (happy-path simplicity); `ManagementOptions.Mongo` still carries
optional `Username`/`Password` so a secured deployment needs only configuration. The shared
`IMongoClient` is registered once (thread-safe, connection-pooled) and the real
`MongoConnectivityCheck` pings the store (`{ ping: 1 }`) behind the `IMongoConnectivityCheck`
port to back the readiness check (anti-stub).

## Run & verify

- `tilt up` brings up `mongodb` + `management`; `/health/ready`
  (http://localhost:8083/health/ready) turns healthy once MongoDB is reachable, `/health/live`
  answers as soon as the process is up.
- Inspect with mongosh: `docker exec -it race-tracker-mongodb mongosh --eval "db.adminCommand('ping')"`.
- Tests: `dotnet test RaceTracker.Management.sln` (the Mongo connectivity integration test needs Docker).

## Not here yet (later stories)

Single-user auth (seeded user, login, hashed passwords, secure-by-default) → **5.2**.
Vehicle CRUD + generic `Controller<T>`/`CrudService<T>`/`Repository<T>` + Unit of Work + registry → **5.3**.
Device discovery (lazy `pending`) + claim → **5.4**. REST → MQTT command dispatch → **5.5**.
