# ADR-0004 — Concurrency: DB constraint for inserts, xmin + ETag for updates

- Status: Accepted · Date: 2026-10-19

## Context
Two kinds of races: (a) two bookings for the same slot; (b) two users modifying the same appointment (cancel vs reschedule).

## Decision
- (a) is handled by the exclusion constraint (ADR-0002) → 409.
- (b) uses PostgreSQL's system column `xmin` mapped as an EF Core concurrency token (no extra column). A stale update raises
  `DbUpdateConcurrencyException` → 409 `CONCURRENCY_CONFLICT`.
- From M3, `GET /appointments/{id}` returns `ETag: "<xmin>"`; cancel/reschedule accept `If-Match`. A mismatch is detected before
  the update → 412 `PRECONDITION_FAILED`. Clients without `If-Match` still get xmin protection.

## Consequences
+ No lost updates, no locks held across requests, standard HTTP semantics.
− Clients must refresh after a 409/412 (the UI does this automatically).
