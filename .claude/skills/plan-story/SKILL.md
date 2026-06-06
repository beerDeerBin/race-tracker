---
name: plan-story
description: >-
  Plan and implement one numbered user story from doc/USER_STORIES.md for the
  race-tracker project. Use when the user runs /plan-story <number> (e.g.
  "/plan-story 3.3") or asks to plan/implement a specific user story by its
  number. Reads the story plus every cross-cutting part of the docs (guiding
  principles, Pflichtenheft references, architecture principles, dependency
  order, open /Oxx/ decisions, PROTOCOL.md where relevant), plans it in plan
  mode (always with unit tests, never e2e), saves the plan to doc/plans/, then
  on approval implements it and runs stack-aware typecheck + lint + format +
  unit tests.
---

# plan-story

Turn a single numbered user story into a reviewed plan, then build and verify it.
The user invokes this as `/plan-story <number>` (e.g. `/plan-story 5.5`). The
number argument is the story id `X.Y` from [doc/USER_STORIES.md](../../../doc/USER_STORIES.md).

The plan is presented and discussed in **English**, even though the source docs
are German.

## 0. Resolve the input

- The argument is a story id like `3.3`, `5.5`, `7.2`. If it's missing,
  malformed, or doesn't exist in the doc, **stop and ask** which story to plan
  (list the nearest valid ids). Don't guess.
- Find the story in [doc/USER_STORIES.md](../../../doc/USER_STORIES.md). Stories
  are headed `### X.Y — Title · <Prio>` where Prio is **M** (Muss), **S** (Soll),
  or **K** (Kann). Milestones are headed `## MN — …`.

## 1. Gather context (read-only — this is the "scan the rest of the doc" step)

Do **not** plan from the story heading alone. A story is only complete together
with the cross-cutting material. Read and fold in:

1. **The target story** — its "Als … möchte ich … damit …", its **Verweise**
   (references) and its **AK** (Akzeptanzkriterien / acceptance criteria). The AK
   are the definition of done — every plan and its tests must cover them.
2. **The guiding principles that apply to *every* story** — the
   `### Leitprinzipien` and `### Bestehender Stand` blocks near the top of
   USER_STORIES.md. In particular the **anti-stub/anti-mock** rule (build the real
   adapter; mocks only in unit tests) and the **anti-refactor "einmalig
   festgezurrt"** rules (GUID is the cross-service correlation key; message
   contracts/DTOs defined once at their boundary; generic CRUD base classes are
   created in M5 and reused; the realtime/notification service is created in M6
   and only extended in M8). Honour these so you don't build something that a
   later story forbids re-doing.
3. **The story's milestone intro** — its *Ziel* and *Voraussetzung*.
4. **The dependency overview** (`## Abhängigkeits-Überblick`, backward-only).
   Treat all lower-numbered stories as **already done** — build on them, don't
   re-create them. Never depend on a higher-numbered story.
5. **Every referenced Pflichtenheft id** — resolve each `/.../` token to its text
   in [doc/PFLICHTENHEFT.md](../../../doc/PFLICHTENHEFT.md) using the map below,
   and pull the relevant data fields (§6 `/Dxx/`) and quality bars (§7–§8).
6. **Architecture principles** — [doc/ARCHITECTURE_PRINCIPLES.md](../../../doc/ARCHITECTURE_PRINCIPLES.md):
   the 4-layer template (Domain → Application → Infrastructure → Api), ports in
   Application / adapters in Infrastructure, Options pattern, observability
   (structured logs, correlation id, live/ready health), and the testing strategy
   (§9). The plan's structure must follow this.
7. **PROTOCOL.md** — [components/race-tracker-mcu/PROTOCOL.md](../../../components/race-tracker-mcu/PROTOCOL.md)
   — only when the story touches the wire format (MQTT decode/encode, commands,
   status/data structs, ODR/time): M2 and M5 especially. Byte layouts must match
   it exactly.
8. **Open decisions** — scan for `📌 Offen (… /Oxx/)` notes and §13.1 of the
   Pflichtenheft. If an open decision affects this story (e.g. `/O60/` for 5.4,
   `/O70/` for 8.4, `/O80/` library choice in M2), surface it: either follow the
   doc's stated current choice, or ask if there is no default.
9. **Later stories that build on this one** — skim forward so you build it
   extensibly enough for them, **without** over-engineering beyond this story's AK.
10. **IDEA.txt** / **README.md** only if you still need background.

## 2. Determine the stack (drives tests + verification)

| Milestone | Stories | Stack | Notes |
|---|---|---|---|
| M1 | 1.x | Infra + .NET | Tilt/Compose, RabbitMQ, solution & layer conventions |
| M2 | 2.x | .NET | Gateway / ingestion |
| M3 | 3.x | .NET | Persistence write + TimescaleDB |
| M4 | 4.x | .NET | Persistence read / GraphQL (HotChocolate) |
| M5 | 5.x | .NET | Management + MongoDB + auth + command send |
| M6 | 6.x | .NET | Realtime / SignalR |
| M7 | 7.x | **React + TypeScript** | The only frontend milestone |
| M8 | 8.x | .NET | Events/rules/notifications — **extends** the M6 service |

## 3. Ask if unclear

If anything blocking is ambiguous after reading (an open `/Oxx/` decision with no
default, a contract field that isn't pinned, which existing project a story should
extend), use a short clarifying question **before** finalizing the plan. Don't ask
about things the docs already answer.

## 4. Plan it (in plan mode)

Do all of the above research **read-only, in plan mode** (enter plan mode if not
already in it; make no edits yet). Then build the plan and present it with the
plan-approval tool (ExitPlanMode). The plan must contain, in English:

- **Story** — id, title, priority, and the "Als … möchte ich … damit …" restated.
- **Acceptance criteria** — the AK, as a checklist the implementation will satisfy.
- **Assumed-done prerequisites** — which lower-numbered stories / infra this builds on.
- **Cross-cutting constraints that apply here** — the specific Leitprinzipien /
  anti-refactor rules and quality bars (§7–§8) relevant to this story.
- **Open decisions** — any `/Oxx/` affecting this story and the choice taken.
- **Implementation steps** — concrete, ordered. For a .NET service, organize by
  layer (Domain → Application → Infrastructure → Api) with ports in Application
  and adapters in Infrastructure, DI extension per layer, Options pattern,
  health live/ready, structured logs + correlation id. For the M7 frontend,
  follow `services → hooks → components`, one configured HTTP client with
  interceptors, typed models, i18n. Name the real files/projects you'll create.
- **Unit tests (always)** — list the unit tests that prove the AK, with ports
  mocked (per architecture §9). **No e2e tests.** Higher-fidelity integration
  tests against real dependencies in throwaway containers are in scope only when
  the AK explicitly needs the real technology (e.g. SQL/Timescale, decode
  round-trip) — keep them few.
- **Verification step** — the stack-aware commands from §6 that will run at the end.
- **Out of scope** — what this story deliberately defers to a later one.

## 5. Save the plan

After the plan is approved (and only then — Write is unavailable in plan mode),
write it to `doc/plans/story-<number>.md` (e.g. `doc/plans/story-3.3.md`),
creating `doc/plans/` if needed. This is the persistent record for the build.

## 6. Implement, then verify

After approval, implement the story per the plan, then run the **stack-aware**
verification and report real results (paste failures; never claim green without
running). Prefer a project script (`npm run lint`, etc.) if one is defined;
otherwise use the raw command. If the tooling doesn't exist yet (early stories —
no solution / no package.json), **setting it up is part of the work**, and
verification runs once it exists.

| Step | .NET backend (M1–M6, M8) | Frontend (M7) |
|---|---|---|
| Format | `dotnet format` | `npx prettier --write .` |
| Lint | analyzers via build; `dotnet format --verify-no-changes` | `npx eslint .` |
| Typecheck | `dotnet build` (must succeed, no new warnings) | `npx tsc --noEmit` |
| Unit tests | `dotnet test` | `npm test` |

Run format first (so it can't fail the build), then typecheck/lint, then tests.
Append a short **Verification result** section (pass/fail + command output
summary) to the saved `doc/plans/story-<number>.md`.

## Reference — Pflichtenheft id → section

| Token | Section in PFLICHTENHEFT.md |
|---|---|
| `/Zxx/` | §1 Zielbestimmung (Muss/Soll/Kann/Abgrenzung) |
| `/Fxx/` | §5 Produktfunktionen (functional requirements) |
| `/Dxx/` | §6 Produktdaten (data model / entity fields) |
| `/Lxx/` | §7 Produktleistungen (non-functional limits) |
| quality | §8 Qualitätsanforderungen (reliability/observability/security/testability) |
| `/Uxx/` | §9 Benutzungsoberfläche (frontend) |
| `/Sxx/` | §10.3 Schnittstellen (interfaces; `/S10/` = PROTOCOL.md) |
| `/Axx/` | §11 Architektur- & Entwicklungsvorgaben |
| `/Oxx/` | §13 / §13.1 Getroffene & offene Entscheidungen |

## Guardrails

- **Plan mode for research; no edits until the plan is approved.**
- **Always plan unit tests; never e2e.**
- **Real adapters, not stubs/fakes** (anti-stub/anti-mock); mocks live only in unit tests.
- **Backward-only dependencies** — assume lower-numbered stories are done; never pull work from a higher-numbered one.
- **Match PROTOCOL.md byte-for-byte** for any wire-format work.
- **Don't re-create the "einmalig festgezurrt" things** (GUID key, one-time contracts/DTOs, M5 generic CRUD, M6 realtime service) — define-once / reuse.
