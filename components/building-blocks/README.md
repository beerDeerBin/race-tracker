# building-blocks — shared kernel (`RaceTracker.BuildingBlocks`)

Cross-service building blocks created in **story 2.1** and reused by every backend
service from M3 onward via `<ProjectReference>` (Leitprinzip *einmalig festgezurrt* —
never re-extracted). See [`doc/CONVENTIONS.md`](../../doc/CONVENTIONS.md) §1–§5.

| Concern | Type | Use in a service |
|---|---|---|
| Structured logging (`/A80/`) | `SerilogConfiguration.UseRaceTrackerSerilog(name)` | call on the host builder in `Program.cs` |
| Correlation id (`/A80/`) | `CorrelationIdMiddleware` + `UseCorrelationId()` | first middleware in the pipeline |
| Health endpoints (`/A80/`) | `HealthEndpoints.MapRaceTrackerHealthChecks()` + `ReadyTag` | map endpoints; tag dependency checks `ReadyTag` |

- `/health/live` — liveness, no dependency checks (always 200 while the process runs).
- `/health/ready` — readiness, runs only checks tagged `HealthEndpoints.ReadyTag`,
  so a downed dependency fails readiness **without** killing the process.

It targets the ASP.NET shared framework (`Microsoft.AspNetCore.App`) because it ships
middleware and endpoint helpers.
