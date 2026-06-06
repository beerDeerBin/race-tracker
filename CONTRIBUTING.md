# Contributing to race-tracker

Thanks for working on **race-tracker** — a battery-powered IMU acquisition system
(ESP32 node → MQTT → .NET backend → web client). This guide is for the project
team: how to set up your machine, how we build features, and the conventions a
change has to satisfy before it lands on `main`.

If you're new here, read these three documents first — this guide assumes them:

- [`README.md`](./README.md) — what the system is and how to run the stack.
- [`doc/ARCHITECTURE_PRINCIPLES.md`](./doc/ARCHITECTURE_PRINCIPLES.md) — the *why*
  behind the design (layering, ports/adapters, service archetypes).
- [`doc/CONVENTIONS.md`](./doc/CONVENTIONS.md) — the **binding** repo/solution/layer
  rules every backend change must follow.

---

## 1. Prerequisites

Install the toolchain for the parts you touch — you don't need all of it for every
change.

| Tool | Version | Needed for |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | **9.0.314** (pinned in [`global.json`](./global.json), `rollForward: latestFeature`) | backend services |
| [Go](https://go.dev/dl/) | **1.22+** ([`components/simulator/go.mod`](./components/simulator/go.mod)) | the simulator |
| [Docker](https://docs.docker.com/get-docker/) + [Tilt](https://tilt.dev) | latest | running the whole stack locally |
| [arduino-cli](https://arduino.github.io/arduino-cli/) + `esp32:esp32@3.3.8` | as in README | firmware (real hardware only) |
| Python 3 | 3.10+ | the data-viewer / config-editor helpers |

The five root files (`global.json`, `Directory.Build.props`,
`Directory.Packages.props`, `nuget.config`, `.editorconfig`) configure the SDK pin,
shared build properties, central package versions and style rules for **every** .NET
project automatically — you don't wire them up per service.

---

## 2. Run the stack

```sh
cd components/
tilt up
```

This brings up the Mosquitto broker (`:1883`), MQTTX web UI (`:8080`), the Flask
data-viewer (`:5000`), the simulator, and any backend services wired into the
[`Tiltfile`](./components/Tiltfile). The Tilt dashboard is at
http://localhost:10350 and has buttons to manage simulated devices without editing
YAML by hand.

To run a single backend service against local brokers instead of the whole stack:

```sh
dotnet run --project components/gateway/src/RaceTracker.Gateway.Api   # http://localhost:8081
```

---

## 3. How we build features: the story workflow

Work is driven by [`doc/USER_STORIES.md`](./doc/USER_STORIES.md). A few rules from
its *Leitprinzipien* shape everything we do:

- **Numbering is the order of work.** Stories are done strictly top-to-bottom
  (1.1 → 8.x). A story only ever depends on lower-numbered ones — never a later one.
- **Anti-stub / anti-mock.** We build the **real adapter**, not an in-memory
  placeholder to be swapped later. Mocks live **only** in unit tests; integration
  tests run against **real dependencies in throwaway containers**.
- **Conventions are fixed once** (*einmalig festgezurrt*). The layout, message
  contracts, GUID correlation key, and shared building-blocks are set up once and
  **reused** — later stories *add* to them, they don't restructure them.

If you use Claude Code, the repo ships two skills that automate this loop:

- **`/plan-story <number>`** — reads the story plus all cross-cutting docs, writes a
  plan to `doc/plans/`, and on approval implements it with unit tests and runs the
  quality gates.
- **`/review-story [number]`** — reviews the resulting diff against the story's
  acceptance criteria and the architecture principles, then cleans it up.

Plan files under `doc/plans/` are **temporary working records** — they're deleted
once the story is done and should not be committed long-term.

---

## 4. Branching & pull requests

- **`main` is the integration branch.** Don't commit directly to it.
- Branch off `main` for your work. Use a short, descriptive branch name
  (e.g. `gateway-mqtt-subscribe`, `fix-tiltfile-volume`).
- Open a PR into `main`. Keep PRs scoped to **one story or one fix** so they're
  reviewable.
- A PR should: build clean, pass all quality gates (§6), and explain *what* changed
  and *which story / acceptance criteria* it satisfies.
- Get a review before merging. The `/review-story` checklist is a good
  self-review baseline.

---

## 5. Coding conventions

### .NET backend — non-negotiable

The full rules are in [`doc/CONVENTIONS.md`](./doc/CONVENTIONS.md); the essentials:

- **One bounded context = one deployable service**, each with its own
  `RaceTracker.<Service>.sln`. There is **no** repo-wide aggregate solution.
- **Four-layer Clean/Onion template** per service, one project per layer, with
  dependencies pointing **inward only**:

  ```
  Domain         →  (none)              # plain models, enums, rules — zero framework deps
  Application    →  Domain              # use cases, PORTS (IXxx), Options, workers
  Infrastructure →  Application         # ADAPTERS: broker/db/http clients, decoders
  Api            →  Infrastructure + Application   # composition root: DI, transport, health
  ```

  The compiler enforces the direction — an inner project can't reference an outer one.
- **Ports in Application, adapters in Infrastructure**, bound in **one DI extension
  per layer** (`AddApplication`, `AddInfrastructure`). Keep `Program.cs` thin.
- **Configuration only via the Options pattern** with a `Section` constant — never
  ad-hoc magic strings.
- **Observability from the grundgerüst story onward**, reusing
  [`building-blocks`](./components/building-blocks/) (Serilog, correlation-id,
  split `live`/`ready` health) — referenced, never copied or re-implemented.
- **Naming & style are enforced at build** via [`.editorconfig`](./.editorconfig):
  `IPascalCase` interfaces, `_camelCase` private fields, `…Async` suffix,
  file-scoped namespaces. Run `dotnet format` before pushing.

### Simulator (Go) & firmware (C++/Arduino)

These live under `components/` and follow their own ecosystem conventions —
idiomatic `gofmt`-clean Go for the simulator, the existing firmware style for the
ESP32 code. Match the surrounding code.

---

## 6. Quality gates (run before every PR)

A change must pass the gates for the parts it touches.

### .NET

```sh
# from the service folder, e.g. components/gateway/
dotnet build      # analyzers + code style run here; warnings are errors
dotnet test       # unit tests (+ integration tests where present)
dotnet format     # apply / verify .editorconfig formatting
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, `EnableNETAnalyzers`, and
`EnforceCodeStyleInBuild`, so **a warning fails the build**. Production `src/`
projects do not relax this; test projects may.

### Simulator (Go)

```sh
# from components/simulator/
gofmt -l .        # must print nothing
go vet ./...
go build ./...
go test ./...
```

### Firmware

```sh
arduino-cli compile --profile profile-race-tracker
```

> There is no CI pipeline yet — these gates are **run locally** and are a
> precondition for merging. If/when CI is added it will run exactly this set.

---

## 7. Testing strategy

Two tiers, no more (see [`doc/CONVENTIONS.md`](./doc/CONVENTIONS.md) §7 and
[`ARCHITECTURE_PRINCIPLES.md`](./doc/ARCHITECTURE_PRINCIPLES.md) §9):

- **Unit tests** (`*.UnitTests`) — the default tier. Isolate a class by **mocking
  its ports** (NSubstitute), assert with a fluent library (Shouldly). Pure domain
  and application logic, **no infrastructure**.
- **Integration tests** (`*.IntegrationTests`) — kept **few**, only where the real
  technology's behaviour matters (DB functions, binary decode round-trips,
  serialization). They spin up **real dependencies in throwaway containers**
  (Testcontainers).
- **No e2e tests.**

Every behavioural change ships with the tests that cover it. New stories add unit
tests as a matter of course.

---

## 8. Commit messages

We keep commits **free-form** but useful — no strict convention is enforced. Aim for:

- A short, **imperative** summary line (~72 chars): *"add MQTT subscribe adapter"*,
  *"fix Tiltfile volume mount"*.
- Reference the user story when the commit implements one — e.g. mention `US-2.1`
  so history maps back to [`doc/USER_STORIES.md`](./doc/USER_STORIES.md).
- A body when the *why* isn't obvious from the summary.
- One logical change per commit where practical.

---

## 9. Language

- **Code, comments, commit messages, PRs, and engineering docs:** English.
  (Architecture principles, conventions, and component READMEs are all in English.)
- **Specification documents stay in German** — the
  [Pflichtenheft](./doc/PFLICHTENHEFT.md) and
  [user stories](./doc/USER_STORIES.md) are the authoritative German sources and
  are not translated.

---

## 10. Getting help

- **What & why of the design** → [`doc/ARCHITECTURE_PRINCIPLES.md`](./doc/ARCHITECTURE_PRINCIPLES.md)
- **How the backend is structured** → [`doc/CONVENTIONS.md`](./doc/CONVENTIONS.md)
- **What to build next** → [`doc/USER_STORIES.md`](./doc/USER_STORIES.md)
- **The wire protocol** → [`components/race-tracker-mcu/PROTOCOL.md`](./components/race-tracker-mcu/PROTOCOL.md)
- Still stuck? Open an issue or ask the team.

Happy hacking 🏁
