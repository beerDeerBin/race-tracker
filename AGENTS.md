# AGENTS.md

Operational guide for AI coding agents (Claude Code, Cursor, etc.) working in this
repo. Humans: read [`CONTRIBUTING.md`](./CONTRIBUTING.md) — agents should read it
too. This file is the short, agent-facing version of the same rules.

**race-tracker** is a distributed, event-driven IMU acquisition system: an ESP32
node streams binary telemetry over MQTT → a .NET backend (built service-by-service)
→ a web client. A Go simulator stands in for real hardware.

---

## 0. Read these before you touch anything

In this order. Don't plan or edit from a single file's local context:

1. [`CONTRIBUTING.md`](./CONTRIBUTING.md) — workflow, quality gates, conventions.
2. [`doc/CONVENTIONS.md`](./doc/CONVENTIONS.md) — **binding** repo/solution/layer rules.
3. [`doc/ARCHITECTURE_PRINCIPLES.md`](./doc/ARCHITECTURE_PRINCIPLES.md) — the *why*.
4. [`doc/USER_STORIES.md`](./doc/USER_STORIES.md) — what to build, and in what order.
5. [`components/race-tracker-mcu/PROTOCOL.md`](./components/race-tracker-mcu/PROTOCOL.md)
   — the wire format, whenever the change touches MQTT encode/decode.

---

## 1. Use the skills — this is the default path

Implementation work is **story-driven**. Two skills already encode the full
workflow; prefer them over ad-hoc planning and review. They make everyone's life
easier and keep changes conforming to the architecture.

- **`/plan-story <number>`** — plan and implement one numbered story from
  `doc/USER_STORIES.md`. It reads the story *plus all cross-cutting context*
  (guiding principles, Pflichtenheft references, architecture, dependency order,
  open `/Oxx/` decisions, PROTOCOL.md where relevant), plans it in plan mode (always
  with unit tests, never e2e), saves a temporary plan to `doc/plans/`, and on
  approval implements it and runs the stack-aware quality gates.
- **`/review-story [number]`** — the natural follow-up. Reviews the resulting diff
  for bugs, acceptance-criteria gaps, and architecture violations, **reports first
  and asks which fixes to apply**, then applies the chosen ones and re-verifies.

**When asked to implement or plan a specific story** (e.g. "do 3.3", "implement the
gateway subscribe story"), invoke `/plan-story <number>` rather than improvising.
**After implementing**, run `/review-story <number>` before considering it done.

If the request isn't a numbered story (a one-off fix, infra tweak, doc change), you
don't need the skills — but still honour every rule below.

---

## 2. Non-negotiable rules

These come straight from the binding docs. Violating them creates rework a later
story is forbidden to redo (*einmalig festgezurrt*).

- **Four-layer template per service** — `Domain → Application → Infrastructure → Api`,
  dependencies pointing **inward only** (the compiler enforces it). **Ports
  (`IXxx`) live in Application; adapters in Infrastructure.** Api is a thin
  composition root.
- **Anti-stub / anti-mock.** Build the **real adapter** — never an in-memory or fake
  stand-in in product code. Mocks live **only in unit tests**.
- **Define-once contracts.** RabbitMQ message contracts, API DTOs, the GUID
  cross-service correlation key — shaped once at their boundary, then reused. DTOs
  stay separate from domain entities. Don't re-create the M5 generic CRUD base
  classes or stand up a second M6 realtime/push path.
- **Backward-only dependencies.** Treat all lower-numbered stories as done and build
  on them; never pull work from a higher-numbered story.
- **Options pattern only** for config (a `Section` constant, bound once) — no
  magic-string configuration. **One DI extension per layer** (`AddApplication`,
  `AddInfrastructure`).
- **Observability from the scaffold story on** — structured Serilog logs with
  context, correlation id, split `/health/live` + `/health/ready` — reused from
  [`building-blocks`](./components/building-blocks/), never re-implemented.
- **Messaging failure handling** — parse/validation error → reject **without**
  requeue → dead-letter; transient/infra error → reject **with** requeue; manual ack
  + bounded prefetch; catch-log-continue in long loops.
- **PROTOCOL.md is byte-for-byte authoritative** for any wire-format work
  (little-endian, struct packing, units: accel m/s², gyro rad/s, `t = index / odr_hz`).

---

## 3. Quality gates — run them, report real results

A change is not done until the gates for the stack it touches pass. **Never claim
green without running**; paste failures.

| Step | .NET backend | Simulator (Go) |
|---|---|---|
| Format | `dotnet format` | `gofmt -l .` (must be empty) |
| Lint / analyze | `dotnet build` (warnings are **errors**); `dotnet format --verify-no-changes` | `go vet ./...` |
| Build / typecheck | `dotnet build` (no new warnings) | `go build ./...` |
| Unit tests | `dotnet test` | `go test ./...` |

Run **format first** (so it can't fail the build), then build/typecheck, then tests.
.NET commands run from the service folder (e.g. `components/gateway/`); each service
has its own `RaceTracker.<Service>.sln`. There is no CI yet — these gates are the
gate.

**Testing:** unit tests always (mock the **ports**, not the logic under test);
integration tests only where the real technology's behaviour matters, against real
dependencies in throwaway containers; **no e2e tests**.

---

## 4. Working agreements

- **Commits:** free-form but useful — short imperative summary; reference the story
  (`US-x.y`) when the commit implements one. One logical change per commit.
- **Branches/PRs:** branch off `main`; **don't commit directly to `main`**. Keep a
  PR scoped to one story or one fix.
- **Language:** English for code, comments, commits, PRs, and engineering docs.
  German is kept **only** for the spec docs (`PFLICHTENHEFT.md`, `USER_STORIES.md`)
  — don't translate them.
- **Plan files in `doc/plans/` are temporary.** `/plan-story` writes one and
  `/review-story` deletes it at the end of the lifecycle. Don't keep them
  long-term.

---

## 5. Guardrails

- **Don't commit, push, or create branches unless the user asks.**
- **Stay within the requested scope** — fix what the task/story covers; *flag*,
  don't silently fix, unrelated pre-existing issues.
- **Don't restructure the fixed layout.** Later stories *add* services that follow
  the conventions; they never re-lay-out what exists.
- **Ask when a blocking decision is genuinely ambiguous** (an open `/Oxx/` with no
  default, an unpinned contract field) — but don't ask what the docs already answer.
