# PROMPTS

Meaningful prompts used while building this project with Claude Code, and what I did with the output.
Format per entry: **Phase · Prompt (abridged) · Outcome: Accepted / Changed / Rejected — why.**

## Planning

### P-01 · Turn the brief into a spec-driven plan
> "Read the PDF task in this folder. Take all requirements into consideration, make it clear and scalable for adding new
> features, and plan it spec-driven before we start, including all Claude files, skills and documentation."

- **Accepted:** three-layer conflict checking (UI hint → API pre-check → Postgres exclusion constraint), UTC storage with
  clinic-zone slot generation, `TimeProvider` for testable "now", feature folders.
- **Changed:** first draft proposed SQLite + a single API project; I chose PostgreSQL (enables range exclusion constraints) and
  Clean Architecture (4 projects + Worker) to show layering.
- **Rejected:** a unique index on `(doctor_id, start_time)` as the only DB guard — it misses partial overlaps once durations vary.

### P-02 · Expand to a production simulation
> "Add as in-scope all non-functional requirements: authentication and authorization, rate limiting, resilience patterns (retry,
> circuit breaker…), unit tests, k6 scripts, a global idempotency middleware/filter with an idempotency records table, RabbitMQ
> with Wolverine if needed… treat this as a simulation of production."

- **Accepted:** milestone plan where M1 = exact challenge (tagged `v1-challenge`) and M2–M7 add NFRs; Identity + JWT with
  rotating refresh tokens; endpoint filter (not middleware) for idempotency so it applies per route group; Wolverine EF Core outbox.
- **Changed:** proposed Aspire dashboard for observability → switched to OpenTelemetry + Jaeger as requested.
- **Rejected:** MassTransit (licensing) and a Redis distributed lock for idempotency — the unique index on `(user_id, key)` is the lock.

### P-03 · SDLC role agents
> "Create AI agents with specific instructions for each role of the SDLC so the whole life cycle works in ceremony."

- **Accepted:** 10 role agents with explicit inputs/outputs/gates, hand-off through repo files, `/sdlc` skill with a `--fast`
  path for small changes (used for the interview live change).
- **Changed:** merged a separate "spec-reviewer" into `qa-engineer` + `code-reviewer` to avoid overlapping responsibilities.

## Coding

### P-04 · Domain rules test-first
> "Implement M1 domain from specs 001–003: an Appointment aggregate enforcing BR-3/4/5 and slot generation in the clinic's
> time zone. Write xUnit tests named after the AC IDs first, including a DST day."

- **Accepted:** `Appointment.Schedule/Cancel/Reschedule` taking an injected "now"; slot validity checked by comparing against the
  generated slots of that local day (one rule handles grid, hours and DST); `Guid.CreateVersion7()` ids.
- **Changed:** planned name `AvailabilityPolicy` became `DaySchedule` (it is a value describing a day, not a policy object);
  reason validation moved into the aggregate (400 `validation_failed`) instead of duplicating it in the endpoint.
- **Rejected:** a separate `SlotTime` value object and a repository per entity — no behavior to justify them yet.

### P-05 · Database guarantee for overlaps
> "Add the initial EF Core migration and append PostgreSQL exclusion constraints for doctor and patient overlaps
> (btree_gist, tstzrange '[)', only Scheduled rows). Translate the violation into the same 409 codes as the pre-check."

- **Accepted:** raw SQL in the migration, constraint-name → `doctor_conflict`/`patient_conflict` mapping in `AppDbContext.SaveChangesAsync`,
  `xmin` as the concurrency token.
- **Changed:** error factories moved to a shared `AppointmentConflicts` class so the pre-check and the DB translation can't drift.
- **Rejected:** wrapping booking in a SERIALIZABLE transaction — redundant with the constraint and adds retry handling.

### P-06 · Angular schedule feature
> "Build the Angular schedule page: doctor select + date picker, slot grid (Free/Booked/Past/PatientBusy), day list with
> cancel/reschedule, and a book dialog with patient autocomplete, reason and a review step. Never convert time zones in the browser."

- **Accepted:** presentational `SlotGrid` reused by the page and the reschedule dialog; `localStart` from the API for display,
  `startUtc` posted back; a `toIsoDate` helper using local getters so the picked day doesn't shift.
- **Changed:** on a 409 `doctor_conflict` the book dialog now closes and refreshes the grid instead of only showing an error.
- **Rejected:** a NgRx store — three signals per page are enough at this size.

## Debugging / refactoring

### P-07 · 500 instead of 400 for a malformed date
> "AC_001_7 fails: `?date=not-a-date` returns 500, expected 400. Find the root cause."

- **Accepted:** in Development minimal APIs throw `BadHttpRequestException` for binding failures, and `UseExceptionHandler`
  turned it into a 500. The exception handler now maps it to its own status code as ProblemDetails `bad_request`.
- **Rejected:** first suggestion was to bind `date` as `string` and parse manually — hides the real problem and repeats per endpoint.

### P-08 · Is the race test really testing the database?
> "The parallel-booking test passes, but could the application pre-check alone be winning it? Prove the constraint works."

- **Accepted:** added `ExclusionConstraintTests` that save overlapping appointments directly through `AppDbContext`, bypassing the
  service — the DB still rejects them with the right codes, and allows back-to-back and cancelled overlaps.

### P-09 · Keep the Application layer provider-agnostic
> "Patient search: case-insensitive contains on name or exact MRN."

- **Rejected:** the generated `EF.Functions.ILike` — it is Npgsql-specific and would make Application depend on the provider
  (the ArchitectureTests forbid `Npgsql` there). Used `ToLower().Contains()` which EF translates to `lower(...) LIKE`.
- **Changed:** a `Directory` namespace shadowed `System.IO.Directory` under implicit usings → renamed to `Lookups`.
