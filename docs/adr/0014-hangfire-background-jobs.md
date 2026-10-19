# ADR-0014 — Hangfire for background jobs, inside the Api process

- Status: Accepted · Date: 2026-10-19 · Supersedes the separate Worker host from ADR-0001/0007

## Context
The app needs scheduled and recurring work (purging expired refresh tokens now; e-mail sending, 24 h reminders and
idempotency-record cleanup later). A separate Worker project added a second deployable before there was any work for it.

## Decision
- **Hangfire** runs **inside the Api process** (`AddHangfireServer`), storing jobs in the same PostgreSQL database, schema
  `hangfire` (`Hangfire.PostgreSql`). No extra infrastructure.
- Jobs are plain classes in Infrastructure, resolved from DI per run (e.g. `Auth/PurgeExpiredRefreshTokensJob`, hourly).
  Registration lives in `Infrastructure/BackgroundJobs/BackgroundJobsSetup.cs`.
- Dashboard at `/hangfire` in **Development only**, local requests only (browser navigation can't carry the in-memory JWT).
- `BackgroundJobs:Enabled=false` turns off the server and recurring registrations (integration tests run jobs directly).
- **Wolverine + RabbitMQ stay** for domain events (ADR-0007, M5), also hosted in the Api: Wolverine moves *events* between
  components reliably (outbox); Hangfire runs *jobs* — scheduled, recurring, retried, visible in a dashboard.
- `Newtonsoft.Json` is pinned to 13.0.4 centrally because Hangfire.Core references a vulnerable 11.0.1.

## Alternatives
Separate Worker host (better isolation, second deployable — not needed yet); `BackgroundService` + timers (no persistence,
retries or dashboard); Quartz.NET (similar, less built-in UI).

## Consequences
+ One process to run and debug; job state survives restarts; retries and history for free.
− Jobs share CPU/memory with request handling. If they grow heavy, move `AddHangfireServer` to a dedicated host — the job
  classes and storage stay unchanged.
