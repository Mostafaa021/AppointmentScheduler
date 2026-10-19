# 009 — Idempotent writes

| Status | Refined ☑ · Designed ☑ · Built ☐ · Tested ☐ · Reviewed ☐ · Released ☐ · Documented ☐ |
|---|---|
| Milestone | M3 |

## Goal
A client (or network retry) can safely resend any write without creating duplicates, and gets the original answer back.

## Acceptance criteria
- **AC-009-1** All POST/PUT/PATCH/DELETE endpoints except `/auth/*` require header `Idempotency-Key` (1–100 chars, UUID
  recommended) → missing: 400 `IDEMPOTENCY_KEY_REQUIRED`.
- **AC-009-2** First request with a key: a record `(user_id, key)` is inserted as `InProgress` before the handler runs; after the
  handler, status code, body and `Location`/`ETag` headers are stored and the record becomes `Completed`.
- **AC-009-3** Same user + key + same request hash (method + path + body SHA-256) after completion → the stored response is
  replayed byte-for-byte with header `Idempotent-Replayed: true`; the handler does not run again.
- **AC-009-4** Same key with a different method/path/body → 422 `IDEMPOTENCY_KEY_REUSED`.
- **AC-009-5** Same key while the first request is still `InProgress` → 409 `IDEMPOTENCY_REQUEST_IN_PROGRESS` + `Retry-After: 1`.
- **AC-009-6** 5xx responses are not stored (record deleted) so the client may retry; 4xx responses are stored and replayed.
- **AC-009-7** Records expire after 24 h and are purged by a scheduled job.
- **AC-009-8** Keys are scoped per user: two users using the same key do not collide.

## Data changes
`idempotency_records(id, user_id, key, method, path, request_hash, status, response_status_code, response_body jsonb,
response_headers jsonb, created_at_utc, expires_at_utc)`, unique `(user_id, key)`, index on `expires_at_utc`.

## Design
ADR-0006. Implemented as an `IEndpointFilter` applied to mutating route groups; the unique index is the lock (no distributed lock).

## Out of scope
Cross-region replication of keys.
