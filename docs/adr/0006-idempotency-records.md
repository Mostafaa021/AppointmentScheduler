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

## Status (2026-10-19): implemented, amended for controllers
- The API moved to controllers (ADR-0015), so the "global `IEndpointFilter`" became a global MVC **resource filter**
  (`Api/Idempotency/IdempotencyFilter`). It wraps model binding, action and result, so the stored response is exactly what the
  client got. It applies to POST/PUT/PATCH/DELETE except on `[SkipIdempotency]` controllers (auth).
- Handler exceptions are written into the captured response by the global exception handler, so 4xx rule rejections are
  stored and replayed (spec AC-009-6) through the same single error writer.
- **Store:**
  - port `IIdempotencyStore` (Application), implemented by `EfIdempotencyStore` (Infrastructure);
  - the claim is `INSERT … ON CONFLICT (user_id, key) DO NOTHING`, so the unique index is the lock, as decided;
  - nothing is tracked, so the record never joins the handler's unit of work.
- **Response body stored as `text`, not `jsonb`:** jsonb would reorder keys and whitespace and break byte-for-byte replay.
- **Added — stale `InProgress` takeover** after `Idempotency:InProgressTimeoutSeconds` (60), as a compare-and-swap on
  `created_at_utc`, so a crash between handler and completion doesn't block the key until it expires. An expired record that
  isn't purged yet frees the key too.
- Error codes are transport-level, so they live in `ApiErrorCodes`: `IDEMPOTENCY_KEY_REQUIRED` 400, `IDEMPOTENCY_KEY_REUSED`
  422, `IDEMPOTENCY_REQUEST_IN_PROGRESS` 409 + `Retry-After: 1`.
