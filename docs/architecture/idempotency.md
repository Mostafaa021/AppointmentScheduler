# Idempotent writes ✅

A retried POST returns the first answer instead of doing the work twice. [Spec 009](../specs/009-idempotency.md) ·
requirements FR-009-x in [functional.md](../requirements/functional.md#009--idempotent-writes--spec--architecture) ·
[ADR-0006](../adr/0006-idempotency-records.md) · plan 012 (POST only).

## Components
| Layer | Piece | Job |
|---|---|---|
| Web | `core/http/idempotency-key.interceptor.ts` | a fresh UUID per POST outside `/api/v1/auth/`; registered outside the auth interceptor, so a retry after a token refresh resends the same key |
| Api | `Idempotency/IdempotencyFilter.cs` (global MVC resource filter), `IdempotencyFilter.Applies`, `SkipIdempotencyAttribute`, `RequestFingerprint`, `IdempotencyKeyOperationTransformer` (OpenAPI) | scope, claim, run, store, replay |
| Application | `Idempotency/IIdempotencyStore.cs` | the port |
| Infrastructure | `Idempotency/EfIdempotencyStore.cs`, `IdempotencyRecord`, `PurgeExpiredIdempotencyRecordsJob` | `INSERT … ON CONFLICT (user_id, key) DO NOTHING` as the lock; hourly purge |

## Does this request need a key?
```mermaid
flowchart TD
  R[request] --> M{method is POST?}
  M -- no: GET/PUT/PATCH/DELETE --> PASS[run normally<br/>PUT/PATCH/DELETE are idempotent by design]
  M -- yes --> S{"[SkipIdempotency] on the<br/>controller or action?<br/>(auth, read-only POST search)"}
  S -- yes --> PASS
  S -- no --> K{Idempotency-Key header<br/>1–100 chars?}
  K -- no --> E400[400 IDEMPOTENCY_KEY_REQUIRED]
  K -- yes --> T[TryBegin user + key + SHA-256 of method, path, body]
  T --> O{outcome}
  O -- Started --> RUN[run the handler, capture the response]
  O -- Replay --> REP[stored status + body + Location/ETag<br/>+ Idempotent-Replayed: true]
  O -- KeyReused --> E422[422 IDEMPOTENCY_KEY_REUSED]
  O -- InProgress --> E409[409 IDEMPOTENCY_REQUEST_IN_PROGRESS<br/>+ Retry-After: 1]
  RUN --> X{response status}
  X -- 2xx / 4xx --> STORE[store it, Completed]
  X -- 5xx --> REL[release the key — the client may retry]
```

## Lost response, safe retry
```mermaid
sequenceDiagram
  participant C as Client
  participant F as IdempotencyFilter
  participant H as Handler
  participant DB
  C->>F: POST + Idempotency-Key K
  F->>DB: INSERT (user, K) InProgress
  F->>H: run
  H-->>F: 201 body
  F->>DB: UPDATE Completed + response
  F-->>C: 201 (response lost in the network)
  C->>F: retry POST + K
  F->>DB: SELECT (user, K) → Completed, same hash
  F-->>C: 201 same body, Idempotent-Replayed: true
```

## Record lifecycle
```mermaid
stateDiagram-v2
  [*] --> InProgress: first request claims (user, key)
  InProgress --> Completed: handler answered 2xx/4xx — response stored
  InProgress --> [*]: handler answered 5xx — row deleted, key free again
  InProgress --> InProgress: stuck > 60 s (crash) — the next request takes it over
  Completed --> [*]: 24 h later — PurgeExpiredIdempotencyRecordsJob (hourly)
```
Keys are scoped per user, so two users who send the same key never collide.
