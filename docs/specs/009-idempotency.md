# 009 — Idempotent writes

| Status | Refined ☑ · Designed ☑ · Built ☑ · Tested ☑ · Reviewed ☐ · Released ☐ · Documented ☑ |
|---|---|
| Milestone | M3 |

## Goal
A client (or network retry) can safely resend any write without creating duplicates, and gets the original answer back.

## Acceptance criteria
- **AC-009-1** All POST endpoints except `[SkipIdempotency]` ones (`/auth/*`, and a POST that only reads, e.g. a search)
  require header `Idempotency-Key` (1–100 chars, UUID recommended) → missing: 400 `IDEMPOTENCY_KEY_REQUIRED`. PUT, PATCH and
  DELETE are idempotent by design and need no key (changed 2026-10-20, plan 012; before: POST/PUT/PATCH/DELETE).
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
`idempotency_records(id, user_id, key, method, path, request_hash, status, response_status_code, response_body text,
response_headers jsonb, created_at_utc, expires_at_utc)` (migration `AddIdempotencyRecords`; the body is `text`, not `jsonb`, because
replay must be byte-for-byte and jsonb normalises whitespace and key order), unique `(user_id, key)`, index on `expires_at_utc`.

## Design
ADR-0006 (amended). Plan: `.claude/plans/009-sprint-3-reliability.md`.

- **Filter:** `Api/Idempotency/IdempotencyFilter`, a global MVC **resource filter** (the API moved to controllers). It wraps model
  binding, the action and the result, so the stored bytes are what the client received.
  - Scope: POST only (`IdempotencyFilter.Applies`, unit-tested), except on `[SkipIdempotency]` controllers (`AuthController`)
    or actions (a read-only POST such as a search). Plan 012.
  - Exceptions from the handler are written into the captured response by the global `IExceptionHandler`, so rule
    rejections (409/422) are stored and replayed like any other 4xx.
  - Kept headers: `Location`, `ETag`, `Content-Type`. Replays add `Idempotent-Replayed: true`.
- **Store:** port `Application/Idempotency/IIdempotencyStore`, implemented by `Infrastructure/Idempotency/EfIdempotencyStore`.
  - `INSERT … ON CONFLICT (user_id, key) DO NOTHING` claims the key, so **the unique index is the lock**.
  - Reads are `AsNoTracking` and writes are `ExecuteUpdate`/`ExecuteDelete`, so nothing joins the handler's unit of work.
- **Fingerprint:** SHA-256 of method + path-and-query + body (`RequestFingerprint`).
- **Stale `InProgress` takeover:** a record older than `Idempotency:InProgressTimeoutSeconds` (60) is taken over with a
  compare-and-swap, so a crash between handler and completion doesn't block the key for 24 h.
- **Expired but not yet purged:** the key starts over. The hourly Hangfire job `PurgeExpiredIdempotencyRecordsJob` deletes the
  rest (`Idempotency:RecordTtlHours`, 24).
- **Clients:**
  - Angular `idempotencyKeyInterceptor` adds `crypto.randomUUID()` to writes. It sits outside the auth interceptor, so the
    retry after a token refresh reuses the key.
  - OpenAPI marks the header required on every write.

## Test plan
| AC | Test |
|---|---|
| AC-009-1…8 | Manual check against the running API (integration tests removed 2026-10-20, plan 011) |
| interceptor | live: the browser's POSTs carry `Idempotency-Key` (no Angular unit tests, plan 016) |

## Out of scope
Cross-region replication of keys.
