---
name: review-story
description: >-
  Review the git changes from implementing a user story for bugs, correctness
  issues, architecture/acceptance-criteria violations, and cleanups, then clean
  them up. Use after a story is implemented (the natural follow-up to
  /plan-story) when the user runs /review-story [number] or asks to review/check
  the changes/diff for a story, find bugs, or make the implementation clean.
  Diffs the working tree (or branch vs main), reviews against the story's AK +
  saved plan and the project's architecture principles (stack-aware: .NET / TS),
  reports findings grouped by severity, ASKS which to apply before touching code,
  then fixes the chosen ones, re-runs typecheck + lint + format + unit tests, and
  deletes the temporary plan file once the review is done.
---

# review-story

Review the code produced for a user story, find what's wrong, and — with your
go-ahead — clean it up. Pairs with [plan-story](../plan-story/SKILL.md). Invoke as
`/review-story` (review the current changes) or `/review-story <number>` (also
review them against that story's acceptance criteria and saved plan).

Reports and discussion are in **English**.

**Fix mode: report, then ask per fix.** Always present findings first and let the
user choose which to apply **before** changing any code. Never auto-fix.

## 1. Establish scope + context

- **Diff scope (auto-detect):**
  - If the working tree has uncommitted changes (`git status --porcelain` is
    non-empty), review those — staged, unstaged, **and untracked new files**.
  - Otherwise review the branch against main: `git diff main...HEAD` plus
    `git log --oneline main..HEAD`.
  - Honour an explicit scope if the user gives one (a ref, a path, "last commit").
- **Story context (if a number was passed):** read the story's **AK** in
  [doc/USER_STORIES.md](../../../doc/USER_STORIES.md) and the saved plan at
  `doc/plans/story-<number>.md`. Review the diff against what it was *supposed* to
  do. If no number was passed, infer the intent from the diff and the most likely
  story; say which you assumed.
- **Stack:** infer from changed files (`.cs` → .NET backend; `.ts`/`.tsx` →
  frontend) and/or the story's milestone. This drives the conformance checks and
  the re-verify commands.

## 2. Understand the change before judging it

Read the **full diff**, and open changed files in full where the diff lacks
context (a hunk rarely shows enough). For wire-format code (MQTT decode/encode,
status/data structs, commands, ODR/units) hold it against
[PROTOCOL.md](../../../components/race-tracker-mcu/PROTOCOL.md) and check the
bytes. Don't review what you haven't actually read.

## 3. Review — in priority order

### a. Correctness & bugs (highest priority)
Logic errors, off-by-one, null/empty handling, wrong/missing error handling,
`async`/`await` misuse and unobserved tasks, resource leaks (undisposed
connections/streams), race conditions, swallowed exceptions. For this project
specifically: **byte layout / little-endian / struct packing** mistakes against
PROTOCOL.md; **wrong units** (accel m/s², gyro rad/s; time `t = index / odr_hz`);
broken **idempotency** (same `guid/runId/index` must not duplicate); wrong
**ack/nack** logic (see error-handling rule below); GUID/`runId` correlation
mishandled.

### b. Acceptance-criteria conformance
Does the change actually satisfy every AK of the story? Call out any AK that is
unmet, partially met, or only mocked.

### c. Architecture conformance (project-specific — check every time)
Against [ARCHITECTURE_PRINCIPLES.md](../../../doc/ARCHITECTURE_PRINCIPLES.md) and
the USER_STORIES Leitprinzipien:
- **4-layer integrity:** Domain has zero framework/IO deps; **ports in
  Application**, **adapters in Infrastructure**; Api stays a thin composition
  root. No inward dependency-direction violation.
- **Anti-stub / anti-mock:** no fake/in-memory adapter standing in for a real one
  in product code; **mocks only in unit tests**.
- **Define-once / anti-refactor:** message contracts & DTOs shaped once at their
  boundary (not duplicated or re-shaped); DTOs separate from entities; GUID is the
  cross-service key; don't re-create the M5 generic CRUD base classes or stand up
  a second M6 realtime/push path.
- **Config & wiring:** Options pattern with section constants (no magic-string
  config); one DI extension per layer; entry point thin.
- **Observability:** structured logs with context, correlation id, health
  live/ready where the story expects them.
- **Resilience / messaging:** **parse or validation error → reject without
  requeue → dead-letter; transient/infra error → reject with requeue**; manual
  ack + bounded prefetch; catch-log-continue in long loops; retry-with-backoff on
  startup deps.
- **Frontend (M7):** `services → hooks → components` layering; one configured HTTP
  client with interceptors (token inject, central 401→logout); typed models
  mirroring contracts; strings externalised (i18n); components don't call the
  network directly.

### d. Tests
Unit tests present and meaningful — they cover the AK and **mock the ports**, not
the logic under test. **No e2e expected.** Flag empty/`skip`ped tests, assertions
that can't fail, and integration tests that don't actually hit the real
dependency they claim to.

### e. Security
Secure-by-default (no unintentionally open endpoints), no secrets/keys committed,
passwords hashed, tokens validated, no stack-trace leaks.

### f. Cleanups / simplification
Dead or commented-out code, leftover debug logging, duplication, unclear names,
magic numbers, unused imports/usings, needless complexity, style that doesn't
match the surrounding file.

## 4. Report findings

Present a single grouped report, **most severe first**:
- **🔴 Bugs / correctness** · **🟠 AC or architecture violations** · **🟡 Cleanups**

Number every finding and give: `path:line` (clickable), what's wrong (one or two
lines), and the concrete fix. Mark each **must-fix** vs **nice-to-have**. If you
find nothing in a category, say so. Be specific and honest — don't invent
findings to pad the list, and don't claim something is fine if you didn't read it.

## 5. Ask which to apply (always — do not skip)

After the report, ask the user which findings to fix before editing anything. For
a handful of findings, offer them via a multi-select question; for many, ask them
to reply with the numbers (e.g. "1, 3, 4", "all bugs", or "all"). Make no code
changes until they choose.

## 6. Apply the chosen fixes

Apply only what was selected. Keep each fix **minimal and in the existing style**;
don't refactor unrelated code or expand scope beyond the diff/story. Re-read each
edited region to confirm the fix is correct in context.

## 7. Re-verify (only if code changed)

Run the **stack-aware** verification and report real results (paste failures;
never claim green without running). Prefer a project script if defined, else the
raw command.

| Step | .NET backend (M1–M6, M8) | Frontend (M7) |
|---|---|---|
| Format | `dotnet format` | `npx prettier --write .` |
| Lint | analyzers via build; `dotnet format --verify-no-changes` | `npx eslint .` |
| Typecheck | `dotnet build` (no new warnings) | `npx tsc --noEmit` |
| Unit tests | `dotnet test` | `npm test` |

## 8. Summarize

Close with: what was found, what was applied, what you intentionally left (and
why), and the verification status. If a story number was given and a plan file
exists, append a short **Review result** section to `doc/plans/story-<number>.md`
as its closing entry — then clean the file up in §9.

## 9. Clean up the plan (it's temporary — don't keep it permanently)

`doc/plans/story-<number>.md` is a **scratch working record** the user does not
keep long-term (it pairs with [plan-story](../plan-story/SKILL.md), which leaves
it for this review to delete). As the **last step of the plan → implement →
review lifecycle**, once the review is complete — chosen fixes applied and
re-verified, summary delivered — **delete the plan file** (and remove
`doc/plans/` if it's now empty), then tell the user you've removed it.

- Only delete the plan for the **story you just reviewed**, and only after the
  summary. If no number was passed or no plan file exists, there's nothing to
  clean up — skip this step silently.
- Don't delete anything else, and don't commit the deletion unless asked.

## Guardrails

- **Report first; never change code before the user picks fixes.**
- **Don't commit, push, or branch** unless the user asks.
- **Stay within the diff/story** — review and fix only what changed; flag, don't fix, unrelated pre-existing issues.
- **Match PROTOCOL.md byte-for-byte** for any wire-format code.
- **Mocks only in unit tests; real adapters in product code.**
- **No e2e tests.**
- **The saved plan is temporary** — after the review, delete `doc/plans/story-<number>.md` (it isn't a permanent doc).
