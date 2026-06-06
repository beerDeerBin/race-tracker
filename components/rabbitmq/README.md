# RabbitMQ — internal service broker (TP-BROKER-AMQP)

RabbitMQ is the **internal message broker** for decoupled pub/sub between the
backend microservices (`/Z30/`, Pflichtenheft §10.2). It is intentionally separate
from Mosquitto: **MQTT (Mosquitto) is the device transport** (telemetry in,
commands out), **RabbitMQ is the service-to-service broker**.

It is brought up by the Tilt dev stack alongside Mosquitto and the simulator — see
[`../Tiltfile`](../Tiltfile), which loads this `docker-compose.yml` together with
[`../mqtt/docker-compose.yml`](../mqtt/docker-compose.yml) as one merged project.

## Connection

| What | Value |
|---|---|
| In-network host (other containers) | `rabbitmq` |
| AMQP port | `5672` |
| Management UI | <http://localhost:15672> |
| Username / password | `race` / `race` *(dev only — see below)* |
| Vhost | `race-tracker` |

A **dedicated user** is used instead of the built-in `guest`, because RabbitMQ
only allows `guest` to connect over loopback; containerised services reaching the
broker over the compose network need a real user. These are **dev-only**
credentials for local Tilt/Docker use (`/ZA10/` — Lern-/Demobetrieb, no
TLS/hardening).

## Exchange / routing convention

> **Status: authoritative — implemented in story 2.3.** The exchanges below are
> declared **idempotently by the gateway producer at startup** (not pre-created
> here), and the message contracts (`/S50/`) are defined **once** as typed records
> in
> [`RaceTracker.BuildingBlocks.Contracts`](../building-blocks/src/RaceTracker.BuildingBlocks.Contracts/Telemetry)
> — locked in and never re-keyed (Leitprinzip: einmalig festgezurrt).

Normalised device telemetry mirrors the two MQTT topic families
(`rt/<guid>/status`, `rt/<guid>/data`) onto two durable **topic exchanges**:

| Exchange | Type | Routing key | Carries |
|---|---|---|---|
| `rt.status` | topic (durable) | device `guid` | normalised status events (`/D30/`) |
| `rt.data` | topic (durable) | device `guid` | normalised sample-batch events (`/D50/`) |

- The device **`guid` is the routing key**, so a consumer can bind to a single
  device (`<guid>`) or to all devices (`#`). The `guid` also stays the
  service-spanning correlation key end to end (no cross-service FKs).
- Messages are **JSON** (`content-type: application/json`), published **persistent**
  with **publisher confirms**; the gateway awaits each confirm in receive order, so
  per-device ordering is preserved (`/L30/`).
- A dead-letter exchange **`rt.dlx`** is reserved for poison-message handling
  (parse/validation failures → reject without requeue → dead-letter, §8); it is
  provisioned by the M3 persistence consumer, **not** here.

## Message contracts (`/S50/`)

Defined once as `RaceTracker.BuildingBlocks.Contracts.Telemetry.*` records and shared
by the producer and every consumer. JSON uses PascalCase property names; `State` is a
string enum (`Idle` / `Connected` / `Acquiring`); `ObservedAtUtc` is the gateway's
ISO-8601 receive time. Run config (`odr` / ranges / `numSamples`) is **not** part of
telemetry — it originates from the `START_RUN` command (M5), not the device payloads.

**`StatusEvent`** → exchange `rt.status`, routing key = `DeviceGuid` (from `/D30/`):

| Field | Type | Notes |
|---|---|---|
| `DeviceGuid` | string | device UUID = routing key + correlation key |
| `UptimeMs` | uint | ms since last boot / RESET |
| `BatteryMv` | ushort | mV; `65535` = unknown |
| `BatteryPct` | byte | 0–100; `255` = unknown |
| `State` | string enum | `Idle` / `Connected` / `Acquiring` |
| `SampledCount` | uint | samples so far this run (0 outside a run) |
| `TotalSamples` | uint | samples requested this run (0 outside a run) |
| `ErrorCode` | ulong | 64-bit error bitmask (PROTOCOL §5.1) |
| `ObservedAtUtc` | datetime | gateway receive time (UTC) |

**`SampleBatchEvent`** → exchange `rt.data`, routing key = `DeviceGuid` (from `/D50/`):

| Field | Type | Notes |
|---|---|---|
| `DeviceGuid` | string | device UUID = routing key + correlation key |
| `RunId` | string | run UUID echoed from the batch header |
| `StartOffset` | uint | absolute index of the first sample in the run |
| `Count` | uint | number of samples in `Samples` |
| `Samples` | array | `{ Ax, Ay, Az, Gx, Gy, Gz }` floats (m/s², rad/s) |
| `ObservedAtUtc` | datetime | gateway receive time (UTC) |

A sample's absolute run index is `StartOffset + position`, so each batch upserts
independently; the time base is derived downstream from the run's ODR
(`t = index / odr_hz`, `/F54/`).

## Run / verify locally

```sh
# Validate the merged dev-stack compose (no daemon needed)
docker compose -f mqtt/docker-compose.yml -f rabbitmq/docker-compose.yml config

# Bring just the broker up
docker compose -f mqtt/docker-compose.yml -f rabbitmq/docker-compose.yml up -d rabbitmq

# Or bring the whole stack up
tilt up

# Checks
docker ps                                            # race-tracker-rabbitmq healthy
curl http://localhost:15672                          # management UI (login page)
docker exec race-tracker-rabbitmq rabbitmqctl list_vhosts   # → race-tracker
docker exec race-tracker-rabbitmq rabbitmqctl list_users    # → race
```
