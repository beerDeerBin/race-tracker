# gateway — Ingestion / Gateway service (TP-GW)

The stateless edge service that will subscribe to the device MQTT topics, decode the
binary telemetry and republish normalised events to RabbitMQ. **Story 2.1 builds the
scaffold only** — the 4-layer service runs with observability and broker-aware health;
subscribe/decode is **2.2**, RabbitMQ republish is **2.3**.

## Layout (4-layer template, `doc/CONVENTIONS.md`)

```
src/RaceTracker.Gateway.Domain          # plain models (empty for now; archetype b, no DB)
src/RaceTracker.Gateway.Application     # GatewayOptions + connectivity ports (IXxx)
src/RaceTracker.Gateway.Infrastructure  # real MQTT/RabbitMQ adapters + health checks
src/RaceTracker.Gateway.Api             # composition root: Serilog, correlation, health
tests/RaceTracker.Gateway.UnitTests     # xUnit + NSubstitute + Shouldly
```

Shared observability (Serilog, correlation id, split health endpoints) comes from
[`building-blocks`](../building-blocks/) — referenced, not copied.

## Health & observability (`/A80/`)

- `GET /health/live` — liveness, no dependency checks.
- `GET /health/ready` — readiness, gates on **real** MQTT **and** RabbitMQ reachability
  (anti-stub: the checks open a short-lived connection to each broker).
- Structured Serilog logs + `X-Correlation-ID` middleware on every request.

## Decisions

- **/O80/ → RabbitMQ.Client** (official low-level client), pinned centrally in
  `Directory.Packages.props` and used by all later services. MQTT uses **MQTTnet**.

## Run

```sh
# Whole stack (brokers + simulator + gateway):
tilt up
curl http://localhost:8081/health/live    # 200
curl http://localhost:8081/health/ready    # 200 once mosquitto + rabbitmq are up

# Just this service locally (Development → localhost brokers):
dotnet run --project src/RaceTracker.Gateway.Api   # http://localhost:8081
```

In Tilt the container reaches the brokers as `mosquitto:1883` and `rabbitmq:5672`
(vhost `race-tracker`); `appsettings.Development.json` points at `localhost` for local runs.
