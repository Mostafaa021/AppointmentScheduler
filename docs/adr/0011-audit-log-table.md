# ADR-0011 — Audit log: append-only `audit_records` table written in the use case's unit of work

- Status: Accepted (human approval 2026-10-19) · Date: 2026-10-19 · Spec: [004](../specs/004-auth.md) (AC-004-24, AC-004-25)

## Context
Spec 004 requires exactly one audit record per auth / appointment / user-admin event, carrying acting user, action, entity,
`TimeProvider` timestamp and request trace id. Rejected requests (4xx) must create no `appointment.*` / `user.*` record, and
failed logins (401/423) must create one even though nothing else is saved. No audit read API exists yet (out of scope).

## Options
1. **Table in the app database, written explicitly by the use case** (`AuditLog.Record(...)` adds a row to the same
   `DbContext`; the use case's single `SaveChangesAsync` commits both).
2. **Structured log events only** (e.g. `LogInformation("audit {Action} …")`, later shipped by Serilog).
3. **EF Core `SaveChanges` interceptor** that derives audit rows from tracked entity changes.
4. **Outbox message / separate audit store** (RabbitMQ → worker → store).

## Decision
Option 1. `public.audit_records(id uuid v7, user_id uuid null, action, entity_type, entity_id null, timestamp_utc, trace_id)`,
no foreign keys (records outlive users and cross the `identity` schema), no indexes beyond the PK until a reader exists.
Rows are only ever inserted — no code path updates or deletes them.

- vs 2: logs are not durable or queryable per entity, are sampled/rotated, and cannot be atomic with the change.
- vs 3: an interceptor sees row changes, not intent — it cannot tell `cancelled` from `rescheduled` without guessing, and
  login failures change no entity. Explicit calls are one line per event.
- vs 4: the outbox arrives in M5 (ADR-0007); an extra hop buys nothing for a same-database write.

## Consequences
+ Atomic: the audit row and the business change commit or fail together, so "no record on rejection" holds by construction
  (a 409 from the exclusion constraint rolls back both).
+ Testable with a plain SQL query in integration tests.
− Same database as the data it audits (a DB admin could edit it). Acceptable for the simulation; tamper evidence (hash chain
  or WORM store) is a later concern.
− Table grows unbounded; retention/export is explicitly out of scope (spec 004).
