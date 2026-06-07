# Software Architecture & Design Principles

A distilled, **project-agnostic** reference of the architectural patterns, design
principles, and technology choices used across this codebase. The intent is to
capture *how the system is built and why*, in reusable language, so the same
principles can be applied to a new project without dragging along any
domain-specific naming.

The system is a **distributed, event-driven platform**: several independent
backend services plus a single-page web client, wired together by a message
broker, HTTP/GraphQL APIs, and real-time push. Each service is small, owns its
own data, and is independently deployable.

---

## 1. High-Level Shape

```
   Devices / Sources                  Web / Mobile Client (SPA)
          │                                     ▲
          │ (telemetry)                         │ (REST + GraphQL + WebSocket)
          ▼                                     │
 ┌──────────────────┐   broker   ┌───────────────────┐
 │ Ingestion / Edge │──────────▶│ Persistence /      │
 │ Gateway service  │  (pub)     │ Time-series store  │◀── query (GraphQL)
 └──────────────────┘            └───────────────────┘
          │                                ▲
          │ registry lookup                │ poll latest
          ▼                                │
 ┌──────────────────┐            ┌───────────────────┐
 │ Core Domain /    │◀───────────│ Events / Rules /   │──▶ realtime push (WS)
 │ Management (CRUD)│  service    │ Notification svc   │
 └──────────────────┘   call     └───────────────────┘
```

The platform is organised around a few **service archetypes** (Section 4). What
makes the design coherent is that *every* service is built from the same
internal template (Section 3) and communicates through a small set of
well-defined integration patterns (Section 5).

---

## 2. Guiding Principles (the "why")

1. **Separation of concerns at every level** — code is split by *responsibility*
   (domain logic vs. orchestration vs. I/O) and by *service* (each bounded
   context is its own deployable). Nothing mixes business rules with transport
   or storage details.
2. **Dependencies point inward.** Inner layers (business rules) never depend on
   outer layers (frameworks, databases, brokers). The outside depends on the
   inside, never the reverse.
3. **Program to interfaces, not implementations.** The core defines *ports*
   (interfaces); the edges provide *adapters* (concrete implementations).
   Anything external — a database, a broker, an HTTP API — sits behind an
   interface so it can be swapped or mocked.
4. **Each service owns its data.** No shared database. Services integrate
   through APIs and messages, never by reaching into each other's tables. This
   lets each service pick the storage technology that fits its job.
5. **Loose coupling via asynchronous messaging.** Producers and consumers don't
   know about each other; they meet at a broker. This absorbs load spikes and
   lets services fail and recover independently.
6. **Favour composition and generics over duplication.** Shared behaviour
   (CRUD, base controllers, base repositories) is written once as a generic and
   specialised where needed.
7. **Make it observable and operable.** Structured logs, correlation IDs,
   metrics, and health checks are first-class, not afterthoughts.
8. **Fail safely and idempotently.** Retries, dead-letter handling, de-dup with
   expiry, and "skip-and-continue" loops mean one bad message or one flaky
   dependency doesn't take the system down or cause duplicate side-effects.

---

## 3. The Per-Service Internal Template (Clean / Onion Architecture)

Every backend service is structured as **four concentric layers**, each a
separate project/assembly. The compiler enforces the dependency direction
because an inner project literally cannot reference an outer one.

```
        ┌─────────────────────────────────────────┐
        │  Api  (composition root / transport)     │  ← entry point, DI wiring,
        │  ┌───────────────────────────────────┐   │    controllers/GraphQL/hubs,
        │  │ Infrastructure (adapters)         │   │    middleware, health, auth
        │  │  ┌─────────────────────────────┐  │   │
        │  │  │ Application (use cases)      │  │   │  ← orchestration, interfaces
        │  │  │  ┌───────────────────────┐  │  │   │    (ports), options, services,
        │  │  │  │ Domain (entities,     │  │  │   │    background workers
        │  │  │  │ models, enums, rules) │  │  │   │
        │  │  │  └───────────────────────┘  │  │   │  ← pure model, no framework
        │  │  └─────────────────────────────┘  │   │    dependencies
        │  └───────────────────────────────────┘   │
        └─────────────────────────────────────────┘
```

### Layer responsibilities

- **Domain** — Plain models, entities, enums, domain exceptions, and
  invariant business rules. **Zero external dependencies.** No framework, no
  database, no serialization concerns leaking in.
- **Application** — The use cases / orchestration. Defines the **ports**: the
  `IXxx` interfaces the service needs from the outside world (a publisher, a
  repository, a state store, a remote client). Contains the application
  services that coordinate domain objects, plus strongly-typed configuration
  (Options) and long-running workers. Depends only on Domain.
- **Infrastructure** — The **adapters**: concrete implementations of the
  Application's ports. This is where the broker client, the database context,
  the cache client, the outbound HTTP/GraphQL clients, decoders, etc. live.
  Depends on Application (to implement its interfaces).
- **Api** — The **composition root** and transport edge. The only place that
  knows about *all* layers. It wires everything via dependency injection,
  exposes the transport (HTTP controllers / GraphQL schema / WebSocket hub),
  and configures cross-cutting middleware, authentication, health checks, and
  observability. Kept deliberately thin.

### Composition via module-level DI extensions

Each layer ships a single extension method that registers everything it owns —
e.g. `AddApplication(configuration)` and `AddInfrastructure(configuration)`.
The entry point then reads almost like a table of contents:

```csharp
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
// + cross-cutting: auth, health checks, metrics, CORS, transport
var app = builder.Build();
```

**Why:** the entry point stays declarative; each layer controls its own wiring;
and you can see a service's entire dependency graph by reading two methods.

### Strongly-typed configuration (Options pattern)

External configuration is never read ad-hoc with magic strings. Each concern
defines an `Options` class with a `Section` constant, bound once at startup and
injected as `IOptions<T>`:

```csharp
services.Configure<BrokerOptions>(configuration.GetSection(BrokerOptions.Section));
// consumed via constructor injection of IOptions<BrokerOptions>
```

**Why:** configuration becomes type-checked, discoverable, testable, and
centralised instead of scattered string keys.

---

## 4. Service Archetypes

The platform decomposes into a handful of recurring service *shapes*. Each is a
template you can reuse.

### a) Core Domain / Management service (system of record)
- Classic **REST CRUD** over the primary business entities.
- Backed by a **document database**; entities use a shared base type with a
  generated identifier.
- Uses **inheritance + discriminators** to model entity hierarchies (a base
  type persisted to one collection, with a type discriminator column selecting
  the concrete subtype).
- Exposes a **registry** other services query to discover what exists.
- Owns **authentication**: issues signed tokens after verifying hashed
  credentials.

### b) Ingestion / Gateway service (stateless edge)
- Subscribes to an inbound transport (e.g. an MQTT/topic stream of telemetry).
- **Decodes / normalises** raw payloads behind an `IDecoder` port.
- **Republishes** clean messages to the broker for downstream consumers.
- Holds **no database** — it is a pass-through pipe with metrics. On decode
  failure it drops the message and records a metric instead of propagating bad
  data.
- Discovers what to subscribe to by calling the registry service at startup
  (with retry).

### c) Persistence / Time-series service (write + read store)
- **Consumes** normalised messages from the broker, **validates** them, and
  **upserts** into a **time-series database**.
- Splits **write path** (ingest/upsert) from **read path** (a **GraphQL** query
  API) — a CQRS-flavoured separation.
- Uses **time-series features**: hypertables for partitioning by time, and
  **continuous aggregates / roll-ups** for pre-computed downsampled views
  (5-minute / hourly / daily buckets) so dashboards query cheap summaries
  instead of raw rows.
- Implements **multi-tenancy as database-per-tenant** with lazy provisioning
  (Section 6).

### d) Events / Rules / Notification service (reactive)
- Runs a **scheduled job** that periodically polls the latest data per entity.
- Evaluates a **rule engine** (a declarative table of predicate → event)
  against each reading.
- De-duplicates alerts with a **cache + TTL idempotency** guard so the same
  condition doesn't notify repeatedly.
- Persists outgoing events via a **transactional outbox**, then a background
  dispatcher pushes them out over **real-time WebSocket** connections.

### e) Web client (SPA)
- A layered single-page app (Section 8) that talks REST + GraphQL for
  request/response and a WebSocket hub for live updates.

**Note on storage diversity:** the platform deliberately uses *different* stores
for different jobs — a document DB for rich domain entities, a time-series DB
for high-volume metrics, a key-value cache for ephemeral state/idempotency, and
a relational DB for the outbox. This is **polyglot persistence**, enabled by the
database-per-service rule.

---

## 5. Inter-Service Communication Patterns

A small, intentional set of integration styles — each chosen to fit the
interaction.

### Asynchronous messaging (the backbone)
- A **message broker** decouples producers from consumers (publish/subscribe and
  work-queue styles).
- Consumers use **manual acknowledgement** with bounded **prefetch** so an
  unacked message is redelivered if a consumer dies, and a slow consumer
  isn't flooded.
- **Dead-letter handling:** unprocessable ("poison") messages are routed to a
  dead-letter exchange/queue rather than being retried forever.
- **Differentiated failure handling on consume:**
  - *Parse/format error* → reject **without requeue** (it will never succeed) →
    dead-letter.
  - *Business validation error* → reject without requeue (caller's fault).
  - *Transient/infrastructure error* → reject **with requeue** (retry later).

### Synchronous request/response
- **REST** for CRUD and simple lookups.
- **GraphQL** for flexible, typed reads of structured/time-series data, where
  the client selects exactly the fields and aggregations it needs.

### Typed HTTP clients
- Outbound calls go through **injected, pre-configured HTTP clients** (base
  address, auth header, service key set once at registration) rather than
  hand-rolled requests scattered through the code.

### Real-time push
- A **WebSocket hub** pushes events to clients. Clients **subscribe to a
  resource group** (one group per entity) so a message is delivered only to the
  interested clients, not broadcast to everyone.

---

## 6. Notable Design Patterns (reusable building blocks)

### Ports & Adapters (Hexagonal)
Already described structurally in Section 3 — the Application defines `IXxx`
ports; Infrastructure supplies adapters; DI binds them. This is what makes the
core testable in isolation and the externals swappable.

### Repository + Unit of Work
- A **generic repository** abstracts persistence behind `GetById / GetAll /
  Find / Add / Update / Delete`.
- A **Unit of Work** groups repositories and owns the single `SaveChanges`
  commit, so a use case can mutate several aggregates and persist atomically.
- **Why:** business logic depends on a storage-agnostic interface; the ORM/DB
  choice stays in one place.

### Generic base classes (DRY through generics + inheritance)
- A **generic base controller** `Controller<T>` implements the five CRUD
  endpoints once; concrete controllers just bind a type and a service.
- A **generic CRUD service** `CrudService<T>` implements the standard flows; a
  **discriminator variant** handles subtypes that share a parent's repository
  (filtering by concrete type).
- **Why:** new entities get a full REST + service stack with almost no
  boilerplate, and the shared behaviour is fixed in one spot.

### Transactional Outbox
- Instead of writing to the DB *and* publishing in one risky step, the producer
  **writes the event to an outbox table** in the same transaction as its state
  change, and a **background dispatcher** later reads pending rows and publishes
  them, marking each processed.
- **Why:** guarantees an event is eventually delivered even if the push target
  is momentarily down — no lost notifications, no dual-write inconsistency.

### Idempotency / de-duplication with TTL
- Before emitting a side-effect (e.g. an alert), the service checks a
  **cache key with an expiry**; if present, it skips; otherwise it acts and
  sets the key with a TTL.
- **Why:** a polling loop that sees the same condition every cycle fires the
  notification once per window, not on every tick.

### Rule engine as data
- Detection rules are a **declarative collection of `(type, predicate,
  message)` tuples** iterated over each reading, rather than a sprawl of
  `if` statements.
- **Why:** adding/adjusting a rule is a one-line data change; the evaluation
  loop never changes.

### Multi-tenancy: database-per-tenant with lazy provisioning
- A **context factory** builds a per-tenant connection from a template, and on
  first use **lazily provisions** the tenant store (run migrations, create
  extensions, set up roll-ups).
- Provisioning is guarded by **double-checked locking** (a per-tenant
  semaphore + an "initialised" set) so concurrent first-requests initialise the
  tenant exactly once.
- **Why:** strong data isolation per tenant without a manual onboarding step.

### Background & scheduled work
- **Hosted/background services** run continuous loops (broker consumers,
  outbox dispatcher).
- A **scheduler** runs periodic jobs; jobs are marked **non-overlapping** so a
  slow run can't stack on top of itself.
- Long loops **catch-log-continue** per item so one failure doesn't abort the
  whole cycle, and honour **cancellation tokens** for graceful shutdown.

### DTOs and mapping
- API/transport types (DTOs) are **separate** from domain entities; a small
  mapping step translates between them.
- **Why:** the wire contract can evolve independently of the internal model,
  and internal fields aren't accidentally exposed.

---

## 7. Cross-Cutting Concerns

### Security
- **Token-based auth** (signed bearer tokens) validated on every protected
  endpoint (issuer, audience, lifetime, signature all checked).
- **Service-to-service auth** via a shared **service-key** scheme for internal
  calls, alongside user tokens.
- A **fallback authorization policy** makes endpoints *secure by default* —
  everything requires an authenticated principal unless explicitly opened.
- **Passwords hashed** with a strong adaptive hash (never stored plaintext).
- Pragmatic edge cases handled deliberately (e.g. accepting a token via query
  string only for resource types that can't send headers).

### Observability
- **Structured logging** with contextual properties (service name, entity id)
  and rolling file + console sinks.
- **Correlation IDs** attached via middleware so a single request/flow can be
  traced across logs.
- **Metrics** exported in a scrape-friendly format (counters for received /
  processed / failed, gauges for counts) and request metrics on the transport.
- **Health checks** split into **liveness** ("am I up?") and **readiness** ("are
  my dependencies reachable?"), with dependency checks tagged so readiness can
  fail without killing the process.

### Resilience
- **Retry with backoff** on critical startup dependencies (e.g. loading the
  registry before subscribing).
- **Automatic reconnection** to broker/cache, plus connection health re-checks
  before use.
- **Graceful degradation:** a failed cycle is logged and skipped, not fatal.

### Middleware pipeline (cross-cutting request handling)
- A consistent ordered pipeline: **correlation-id → global exception handling →
  request logging → CORS → authentication → authorization → endpoints**.
- A **global exception middleware** converts unhandled errors into consistent
  problem responses instead of leaking stack traces.

### Validation at the boundaries
- Input is validated where it enters: a **declarative validation library** for
  API request models, and explicit **domain validation** (throwing typed
  domain exceptions) for messages consumed off the broker. Invalid data is
  rejected early and routed appropriately (4xx response, or dead-letter).

---

## 8. Frontend Architecture (SPA)

The web client mirrors the backend's layering — separation of concerns all the
way to the UI.

```
 pages/         route-level screens, compose components + hooks
   └─ components/   presentational + small container components (incl. tabs, modals)
        └─ hooks/      stateful logic; data fetching, subscriptions, caching per view
             └─ services/   thin API layer: one module per backend resource
                  └─ utils/     shared client (HTTP instance, interceptors, logger, token helpers)
   models/        TypeScript types mirroring API contracts
   context/       cross-cutting app state (e.g. auth) via provider
   i18n/          externalised translations (multi-language)
```

Key practices:
- **A dedicated service layer** wraps every backend resource; components never
  call the network directly.
- **A single configured HTTP client** with **interceptors** that inject the auth
  token on every request, log requests/responses (masking secrets), and handle
  `401` centrally by logging the user out.
- **Custom hooks** encapsulate per-view data fetching and live-update state, so
  components stay declarative.
- **Real-time** updates via a WebSocket connection that auto-reconnects and
  subscribes/unsubscribes to a resource group as the user navigates.
- **Route guards** (protected routes) and an **auth context** gate access.
- **Error boundaries** contain render-time failures.
- **Internationalisation** keeps all user-facing strings external.
- Strong typing end-to-end: **models mirror the API contracts**.

---

## 9. Testing Strategy

- **Unit tests** isolate a class by mocking its ports (substitute/mocking
  library), asserting behaviour with a fluent assertion library. Pure domain
  and application logic is tested without any infrastructure.
- **Integration tests** spin up **real dependencies in throwaway containers**
  (e.g. a real database container) via a test-container library, exercising the
  actual adapters (migrations, queries, roll-ups) end-to-end, then dispose the
  container.
- **Fixtures** manage expensive shared setup (container lifecycle) across a test
  class.
- **Why this split:** fast, deterministic unit tests for logic; a smaller number
  of high-fidelity integration tests for the parts where the real technology's
  behaviour matters (SQL, time-series functions, serialization).

---

## 10. Technology Roles (generic, swappable)

The point is the *role*, not the specific product. Each can be swapped for an
equivalent because it sits behind a port.

| Role | What it does here |
| --- | --- |
| Backend platform | A modern, layered web framework with built-in DI, hosted services, and middleware |
| Message broker | Async pub/sub + work queues with acks, prefetch, dead-lettering |
| Document database | Stores rich domain entities (system of record) |
| Time-series database | High-volume metric storage with partitioning + pre-aggregated roll-ups |
| Key-value cache | Ephemeral state + idempotency keys with TTL |
| Relational database | Backing store for the transactional outbox |
| Query API | A typed, client-driven query language for flexible reads |
| Real-time transport | Server→client push over persistent connections, with groups |
| Scheduler | Periodic, non-overlapping background jobs |
| Inbound telemetry transport | Topic-based stream of device/source data |
| Metrics + logging + health | Scrape-friendly metrics, structured logs, liveness/readiness probes |
| SPA framework + typed language | Component-based client with a typed model layer |

---

## 11. Checklist to Apply These Principles to a New Project

1. **Split by bounded context.** One deployable service per cohesive
   responsibility; give each its own data store.
2. **Inside each service, use the four-layer template** (Domain → Application →
   Infrastructure → Api) and let project references enforce the dependency
   direction.
3. **Define ports in Application, adapters in Infrastructure**, and bind them in
   a per-layer DI extension method. Keep the entry point thin.
4. **Bind all config through the Options pattern** with section constants.
5. **Integrate asynchronously where you can** (broker with acks + dead-letter),
   synchronously where you must (REST/GraphQL via typed clients).
6. **Add reliability patterns where side-effects matter:** transactional
   outbox for must-deliver events, TTL idempotency for must-happen-once
   actions, retries + graceful-skip for flaky dependencies.
7. **Reuse via generics** for CRUD controllers/services/repositories; keep a
   Unit of Work for atomic writes.
8. **Make it operable from day one:** structured logs, correlation IDs, metrics,
   and split liveness/readiness health checks.
9. **Secure by default:** token auth + fallback authorization policy, hashed
   credentials, service keys for internal calls.
10. **Mirror the layering on the frontend:** services → hooks → components, one
    configured HTTP client with interceptors, typed models, real-time via
    auto-reconnecting subscriptions.
11. **Test in two tiers:** fast unit tests with mocked ports; fewer,
    high-fidelity integration tests against real dependencies in containers.
