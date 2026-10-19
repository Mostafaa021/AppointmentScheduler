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
_(entries added with `/log-prompt` during implementation)_

## Debugging / refactoring
_(entries added with `/log-prompt` during implementation)_
