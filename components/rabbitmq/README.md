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

> **Status: proposal.** This documents the convention so M2 can adopt it. The
> **authoritative** RabbitMQ message contracts (`/S50/`) and the exchange / queue /
> binding **declarations** are defined in **M2** (story 2.3) and declared
> idempotently by the producer at startup — they are **not** pre-created here, so
> the contract is locked in once and never re-keyed (Leitprinzip: einmalig
> festgezurrt).

Normalised device telemetry mirrors the two MQTT topic families
(`rt/<guid>/status`, `rt/<guid>/data`) onto two durable **topic exchanges**:

| Exchange | Type | Routing key | Carries |
|---|---|---|---|
| `rt.status` | topic | device `guid` | normalised status events (`/D30/`) |
| `rt.data` | topic | device `guid` | normalised sample-batch events (`/D50/`) |

- The device **`guid` is the routing key**, so a consumer can bind to a single
  device (`<guid>`) or to all devices (`#`). The `guid` also stays the
  service-spanning correlation key end to end (no cross-service FKs).
- A dead-letter exchange **`rt.dlx`** is reserved for poison-message handling
  (parse/validation failures → reject without requeue → dead-letter, §8); it is
  provisioned by the M3 persistence consumer, not here.

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
