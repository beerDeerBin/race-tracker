# Repo, Solution & Layer Conventions

> **Status: binding.** These conventions are fixed **once** and reused by every
> backend service (Leitprinzip *einmalig festgezurrt*). They derive from
> [`ARCHITECTURE_PRINCIPLES.md`](./ARCHITECTURE_PRINCIPLES.md) (`/A10/`, `/A20/`,
> `/A30/`, `/A40/`, `/A70/`, `/A80/`) and are the output of **user story 1.2**.
> No later story restructures the layout — it only *adds* services that follow it.

This document covers the **.NET backend**. The Go simulator, the MQTT/RabbitMQ
infra, and the (future) React frontend (M7) live alongside under `components/` but
follow their own ecosystem conventions.

---

## 1. Monorepo layout

One **bounded context = one deployable service** (`/A10/`), each with its own data
store and its own solution. Services sit next to the existing infra components:

```
race-tracker/
├─ global.json                 # pins the .NET SDK (9.0.x)
├─ Directory.Build.props        # common MSBuild properties for ALL projects
├─ Directory.Packages.props     # Central Package Management (single-sourced versions)
├─ nuget.config                 # pinned package source
├─ .editorconfig                # style + naming, enforced at build
├─ components/
│  ├─ mqtt/                      # Mosquitto (device transport)        — infra
│  ├─ rabbitmq/                  # RabbitMQ (internal service broker)  — infra
│  ├─ simulator/                 # TP-SIM (Go)                         — infra
│  ├─ race-tracker-mcu/          # TP-MCU firmware + PROTOCOL.md        — infra
│  ├─ building-blocks/           # shared kernel (created in 2.1, reused from M3)
│  │  └─ src/RaceTracker.BuildingBlocks/
│  └─ <service>/                 # one folder per .NET service (see §2)
└─ doc/
```

The five root files (`global.json`, `Directory.Build.props`,
`Directory.Packages.props`, `nuget.config`, `.editorconfig`) are **discovered
hierarchically** and apply to every `*.csproj` beneath them — `Directory.Build.props`,
`Directory.Packages.props` and `nuget.config` via the MSBuild/NuGet directory walk-up,
`global.json` via the SDK host, and `.editorconfig` via the compiler/analyzers
(`root = true`). Add a service folder and it gets the SDK pin, the shared build
properties, central versions and the style rules for free.

### Service codename → folder map

| Pflichtenheft codename | Milestone | Folder |
|---|---|---|
| TP-GW (Gateway / Ingestion) | M2 | `components/gateway/` |
| TP-PERS (Persistence: write + GraphQL read) | M3–M4 | `components/persistence/` |
| TP-MGMT (Management / Auth / Registry) | M5 | `components/management/` |
| TP-EVT (Realtime / Notification) | M6, extended M8 | `components/realtime/` |
| TP-FE (Frontend SPA) | M7 | `components/frontend/` |
| — (shared kernel) | created in M2 (2.1) | `components/building-blocks/` |

---

## 2. Per-service structure: four projects, one solution

Every service is the **four-layer Clean/Onion template** (`/A20/`), one project
(assembly) per layer, plus its own `.sln` and test projects:

```
components/<service>/
├─ RaceTracker.<Service>.sln          # this service's own solution
├─ src/
│  ├─ RaceTracker.<Service>.Domain/          # entities, value objects, enums, domain rules
│  ├─ RaceTracker.<Service>.Application/      # use cases, PORTS (IXxx), Options, workers
│  ├─ RaceTracker.<Service>.Infrastructure/   # ADAPTERS: broker/db/http clients, decoders
│  └─ RaceTracker.<Service>.Api/              # composition root: DI wiring, transport, health
└─ tests/
   ├─ RaceTracker.<Service>.UnitTests/
   └─ RaceTracker.<Service>.IntegrationTests/   # only where real tech behaviour matters
```

- **Solution scope:** each service builds and opens **independently** via its own
  `RaceTracker.<Service>.sln`. There is **no** repo-wide aggregate solution.
- **Namespace = assembly name = folder name** (`RaceTracker.<Service>.<Layer>`),
  enforced by `RootNamespace`/`AssemblyName` in `Directory.Build.props`.
- **Shared kernel:** cross-service building blocks (Serilog setup, correlation-id
  middleware, health helpers, base abstractions) live in
  `components/building-blocks/src/RaceTracker.BuildingBlocks` and are referenced by
  **project path** (`<ProjectReference>`). Created in **2.1**, reused thereafter —
  never re-extracted.

### Layer responsibilities & dependency direction (`/A20/`, `/A30/`)

Dependencies point **inward only**; the compiler enforces it because an inner
project cannot reference an outer one. The **only** allowed project references:

```
Domain            →  (none)
Application        →  Domain
Infrastructure     →  Application   (+ Domain transitively)
Api                →  Infrastructure + Application   (+ Domain)
```

| Layer | Holds | Depends on |
|---|---|---|
| **Domain** | Plain models, entities, enums, domain exceptions, invariants. **Zero framework deps.** | nothing |
| **Application** | Use-case/orchestration services, the **ports** (`IXxx` interfaces), `Options` classes, hosted/background workers. | Domain |
| **Infrastructure** | **Adapters** implementing the ports: broker client, DB context, cache, outbound HTTP/GraphQL clients, decoders. | Application |
| **Api** | **Composition root**: DI wiring, HTTP controllers / GraphQL schema / SignalR hub, middleware, auth, health, observability. Kept thin. | Infrastructure + Application |

---

## 3. Dependency Injection: one extension per layer (`/A30/`)

Each layer ships **one** `IServiceCollection` extension that registers everything
it owns, so the entry point reads like a table of contents and each layer controls
its own wiring.

```csharp
// RaceTracker.<Service>.Application/DependencyInjection.cs
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GatewayOptions>(
            configuration.GetSection(GatewayOptions.Section));
        // application services, hosted workers, validators …
        return services;
    }
}

// RaceTracker.<Service>.Infrastructure/DependencyInjection.cs
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // bind ports → adapters (broker client, repositories, http clients) …
        return services;
    }
}
```

```csharp
// RaceTracker.<Service>.Api/Program.cs — thin entry point
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
// + cross-cutting: serilog, correlation-id, health (live/ready), auth, metrics, transport
var app = builder.Build();
```

---

## 4. Configuration: Options pattern only (`/A40/`)

Configuration is **never** read with ad-hoc magic strings. Each concern defines an
`Options` class with a **`Section` constant**, bound once at startup and consumed as
`IOptions<T>`:

```csharp
public sealed class GatewayOptions
{
    public const string Section = "Gateway";

    public required string MqttHost { get; init; }
    public int Prefetch { get; init; } = 50;
}

// binding (in AddApplication / AddInfrastructure):
services.Configure<GatewayOptions>(configuration.GetSection(GatewayOptions.Section));

// consumption: ctor-inject IOptions<GatewayOptions> (or IOptionsMonitor<T> for reload)
```

The matching `appsettings.json` section key equals the `Section` constant.

---

## 5. Observability baseline (`/A80/`)

Applied from each service's grundgerüst story onward (using the shared
building-blocks, not re-implemented per service):

- **Structured logging** (Serilog) with service-name + entity-id context, console +
  rolling file sinks.
- **Correlation-ID middleware** so a request/flow is traceable across logs; the
  device `guid` stays the service-spanning correlation key end to end.
- **Health checks split** into `/health/live` (am I up?) and `/health/ready`
  (are my dependencies reachable?), dependency checks tagged so readiness can fail
  without killing the process.
- **Metrics** in a scrape-friendly format (received / processed / failed counters).

---

## 6. Build infrastructure (root files)

| File | Purpose |
|---|---|
| `global.json` | Pins the SDK (`9.0.314`, `rollForward: latestFeature`) → reproducible builds. |
| `Directory.Build.props` | `net9.0`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, analyzers + `EnforceCodeStyleInBuild`. |
| `Directory.Packages.props` | **Central Package Management**: versions declared once via `<PackageVersion>`; projects reference packages **without** a `Version`. |
| `nuget.config` | Pins `nuget.org` as the single package source. |
| `.editorconfig` | Formatting, file-scoped namespaces, `using` order, naming rules — enforced at build. |

**Open decision `/O80/`:** the .NET RabbitMQ library (RabbitMQ.Client vs.
MassTransit) is chosen in **story 2.1** and its `<PackageVersion>` pinned then — it
is intentionally **not** pre-pinned in `Directory.Packages.props`.

### Naming quick-reference (from `.editorconfig`)

| Symbol | Convention |
|---|---|
| Interfaces | `IPascalCase` |
| Classes / structs / enums / members | `PascalCase` |
| Constants | `PascalCase` |
| Private instance fields | `_camelCase` |
| Async methods | `…Async` suffix |
| Namespaces | **file-scoped** (`namespace X;`) |

---

## 7. Testing convention (`ARCHITECTURE_PRINCIPLES.md` §9)

Two tiers, named as in §2:

- **Unit tests** (`*.UnitTests`) — isolate a class by **mocking its ports**
  (e.g. NSubstitute/Moq), assert with a fluent assertion library. Pure domain and
  application logic is tested with **no infrastructure**. The default tier.
- **Integration tests** (`*.IntegrationTests`) — spin up **real dependencies in
  throwaway containers** (Testcontainers) and exercise the actual adapters. Kept
  **few**, only where the real technology's behaviour matters (SQL/Timescale
  functions, binary decode round-trips, serialization). **No e2e tests.**

Test projects set `IsPackable=false` and may relax `TreatWarningsAsErrors` in their
own folder; production `src/` projects do not.

---

## 8. Device `guid` & `runId` on the wire (binding)

The device **`guid`** is the service-spanning correlation key (Leitprinzip *einmalig
festgezurrt*). It is a UUID **string** that doubles as a **case-sensitive MQTT topic
segment** — `rt/<guid>/{cmd,status,data}` (PROTOCOL §1). The firmware
([`mqtt_mod.cpp`](../components/race-tracker-mcu/src/mqtt/mqtt_mod.cpp)) and the simulator
both format it **upper-case** (`%04X`) and subscribe to their command topic with that exact
casing. MQTT topic matching is case-sensitive, and the device's pre-run validation gives
**no NACK** (PROTOCOL §4.2) — a command sent to a wrongly-cased topic is **silently dropped**.

Binding rules for every service:

- **The gateway does not normalise the `guid`.** It arrives in `StatusEvent.DeviceGuid` /
  `SampleBatchEvent.DeviceGuid` **exactly as it appeared on the topic** (see
  `DeviceTopic.TryParse`). Downstream code must treat it as an **opaque, case-sensitive
  string**.
- **Never round-trip the device `guid` through `System.Guid`** (whose `ToString()`
  lower-cases) when the value will be used to **build a command topic** or as a **storage key
  that must match the topic**. Store it and re-emit it **verbatim**. This is the enforcement
  point for the M5 command dispatch (5.5): build `rt/<guid>/cmd` from the verbatim
  `StatusEvent.DeviceGuid`.
- **`runId`** is generated by the command sender (management, M5) as an ordinary UUID
  (`Guid.NewGuid().ToString()`, lower-case) and converted to/from the firmware's packed
  `uint16[8]` form **only** through the shared
  [`RunIdCodec`](../components/building-blocks/src/RaceTracker.BuildingBlocks.Contracts/Protocol/RunIdCodec.cs)
  (`Encode`/`Decode`), so a run started by management carries the same id later stored from its
  samples — never re-implement the encoding.
- **Storage case-tolerance differs by store, by design:** persistence keys `guid`/`runId` in
  PostgreSQL `uuid` columns, which normalise case, so its GraphQL read API is case-tolerant.
  **MongoDB (management) string keys and MQTT topics are not** — keep the `guid` verbatim there.
