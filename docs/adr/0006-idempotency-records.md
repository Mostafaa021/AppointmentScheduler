# ADR-0006 — Idempotency via `Idempotency-Key` header and an `idempotency_records` table

- Status: Accepted · Date: 2026-10-19

## Context
Network timeouts make clients retry writes. Without protection a retried booking may hit 409 (confusing) or, for non-conflicting
writes (patient create), create duplicates.

## Decision
Global `IEndpointFilter` on all mutating route groups (spec 009):
1. Require `Idempotency-Key`; hash method + path + body (SHA-256).
2. `INSERT … (user_id, key) status=InProgress` — the **unique index is the lock** (no Redis/distributed lock needed).
3. Run the handler; store status code, body (jsonb) and selected headers; mark `Completed`. 5xx → delete the record.
4. Duplicate key: completed + same hash → replay (`Idempotent-Replayed: true`); different hash → 422; in progress → 409.
5. TTL 24 h, purged hourly by a scheduled job.

The Angular client generates one key per user action and reuses it on automatic retries, which makes retrying POSTs safe.

## Consequences
+ Exactly-once effect for clients; standard pattern (Stripe-style); works across API replicas because state is in Postgres.
− One extra insert/update per write (acceptable; measured by k6).
