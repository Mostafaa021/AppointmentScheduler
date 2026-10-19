# 009 — Sprint 3: M3 reliability

- Status: **In progress** · Approved 2026-10-19 · Backlog S-30, S-32, S-33 (S-31 set aside) · Spec 009 · ADR-0006, 0008, 0009
- Progress: Step P ☑ · Step 0 ☐ · S-30 ☐ · S-32 ☐ · S-33 ☐ (ticked as each step is verified; commits only when the user says so)

## Context
Next is Sprint 3 from `docs/backlog.md`. Sprint 4 (clinics, working hours, patient CRUD) is set aside: the challenge has no
detailed requirements for it.

Specs and ADRs to follow:
- spec 009 + ADR-0006 (idempotency);
- NFR-5/6/7 + ADR-0009 (rate limiting);
- S-33's remaining "Npgsql retry".

Your choices:
- one step per story, in order **Step P (plans in the repo) → Step 0 (layering) → S-30 → S-32 → S-33**, each with tests, docs and Claude files;
- **no automatic commits**: at the end of each step I stop, show what changed and ask "Commit it" or "Leave it in the
  working tree". The next step starts only after your answer;
- **S-31 (ETag / If-Match) set aside for now**: it stays "Refined" in the backlog, and `xmin` keeps returning 409
  `CONCURRENCY_CONFLICT` for concurrent updates;
- **output caching skipped**: nothing in the API is safely cacheable yet, and availability must never be stale.

The specs were written for Minimal APIs (`IEndpointFilter`). Now that we have controllers, the same rules become an MVC filter.

## Step P — Plans saved in the repo, one file per plan
Goal: every approved plan is kept as its own file in the repository, so it can be read and amended later. Today they only live
in `~/.claude/plans/` with random names.
- **Directory `.claude/plans/`**:
  - one file per plan, `NNN-<kebab-slug>.md` (e.g. `009-sprint-3-reliability.md`);
  - each starts with a status header: `Status: Draft | Approved | In progress | Done | Superseded by NNN`, the date, and the
    linked backlog stories / specs / ADRs;
  - `.claude/plans/README.md` is the index (number, title, status, link).
- **Claude Code setting**: check whether this version supports the `plansDirectory` setting.
  - If it does, set `"plansDirectory": ".claude/plans"` in the project's `.claude/settings.json`, so plan mode writes there
    directly.
  - Either way, the rule below makes it happen.
- **`CLAUDE.md` workflow rule**:
  - "After a plan is approved, save it as `.claude/plans/NNN-<slug>.md` (next number) and add it to the index before coding.
    When the plan changes during the work, edit that file (don't create a new one), and update its Status as steps finish."
  - The plan for a change is referenced from the spec / backlog story it implements.
- **`/new-feature` and `/sdlc` skills**: one line pointing to the plan file as the first artifact; the architect agent likewise.
- **Backfill**:
  - this Sprint 3 plan becomes `009-sprint-3-reliability.md` (status Approved → In progress);
  - earlier plans (controllers/Hangfire refactor, response envelope, host config) are added as `Done` entries, summarised from
    git history and PROMPTS P-11…P-19, so the index starts complete.

## Step 0 — Api references Application and Infrastructure only, never Domain
Today five Api files use `AppointmentScheduler.Domain` directly:
- `GlobalExceptionHandler` (catches `SchedulerException` / `ValidationException`);
- `AuthSetup`, `BadRequestResponse`, `ErrorStatusMapping`, `ApiResponseProblemDetailsWriter` (use `ErrorCodes`).

`AppointmentDto.Status` also leaks the Domain enum `AppointmentStatus` into the API contract.

- **Exception translation moves to Application:** `Application/Common/ApplicationError.cs` (record `StatusCode, ErrorCode,
  Message, FieldErrors, LogDetail`) with `static ApplicationError? From(Exception)`. It returns null for anything that isn't a
  `SchedulerException`. `GlobalExceptionHandler` calls it and keeps the generic-500 path for null.
- **Transport-level codes move to Api:** `Api/Errors/ApiErrorCodes.cs` with `BAD_REQUEST`, `UNAUTHORIZED`, `FORBIDDEN`,
  `ROUTE_NOT_FOUND`, `METHOD_NOT_ALLOWED`, `UNSUPPORTED_MEDIA_TYPE`, `UNSUPPORTED_API_VERSION`, `INTERNAL_ERROR`. The new
  idempotency and rate-limit codes will go here too.
  - Domain `ErrorCodes` keeps the business codes, plus `VALIDATION_FAILED`, `UNAUTHORIZED` and `FORBIDDEN`, which Domain and
    Infrastructure also throw.
  - The shared `UNAUTHORIZED` / `FORBIDDEN` exist in both classes with identical values, and a test enforces that.
- **DTO contract:** `AppointmentDto.Status` becomes the Application enum `AppointmentStatusDto { Scheduled, Cancelled }`,
  mapped in `AppointmentDto.From`. The JSON stays `"Scheduled"` / `"Cancelled"`, so Angular is unchanged.
- **Architecture tests:**
  - new `Api_does_not_depend_on_domain` (NetArchTest `ShouldNot().HaveDependencyOn("AppointmentScheduler.Domain")` on the Api
    assembly);
  - an `ApiErrorCodes` test: UPPER_SNAKE, unique, and no clash with Domain codes except the two declared shared ones (identical
    values).
  - Project references stay `Api → Application, Infrastructure`. Domain arrives only transitively, which the test makes
    unusable in Api code.
- **Integration tests** switch `ErrorCodes.BadRequest/RouteNotFound/…` to `ApiErrorCodes.…`. Test code may still use Domain codes.
- **Docs:**
  - `CLAUDE.md` layer rule: "Api references Application + Infrastructure only; no Domain types in Api code or in DTOs";
  - architecture.md, ADR-0001 note, ADR-0015 (two code classes);
  - backend + code-reviewer agents, `/new-feature` (where a new code goes: business → Domain `ErrorCodes`, transport →
    `ApiErrorCodes`);
  - CHANGELOG, PROMPTS.

## S-30 — Idempotent writes (spec 009, ADR-0006)
- **Error codes** (`ApiErrorCodes`, "Idempotency" section; transport-level, Api only): `IDEMPOTENCY_KEY_REQUIRED` (400), `IDEMPOTENCY_KEY_REUSED` (422),
  `IDEMPOTENCY_REQUEST_IN_PROGRESS` (409 + `Retry-After: 1`).
- **Port** `Application/Idempotency/IIdempotencyStore`, used by Api, implemented in Infrastructure:
  - `TryBeginAsync(userId, key, method, path, requestHash, now)` → `Started(id) | Replay(record) | InProgress | Mismatch`;
  - `CompleteAsync(id, status, body, headers)`;
  - `ReleaseAsync(id)` (delete).
- **Infrastructure:**
  - entity `Persistence/Idempotency/IdempotencyRecord` and table `idempotency_records`:
    - columns: id, user_id, key, method, path, request_hash, status, response_status_code, response_body **text**,
      response_headers jsonb, created_at_utc, expires_at_utc;
    - unique `(user_id, key)` and an index on `expires_at_utc`;
    - migration via `/add-migration`.
  - **The unique index is the lock**: `INSERT … ON CONFLICT (user_id, key) DO NOTHING` through `ExecuteSqlInterpolatedAsync`.
    It returns 1 → we started; 0 → load the existing row with `AsNoTracking`. Nothing is tracked, so the handler's own
    `SaveChanges` is unaffected.
  - The body is stored as text, not jsonb: replay must be byte-for-byte (spec said jsonb; changed and noted in the spec).
  - An `InProgress` row older than `Idempotency:InProgressTimeoutSeconds` (60) may be taken over, so a crash between handler and
    completion doesn't block the key for 24 h.
- **Api** `Api/Idempotency/IdempotencyFilter : IAsyncResourceFilter`, registered globally in `ApiConfig`.
  - **Scope:** only POST/PUT/PATCH/DELETE, and only on controllers without `[SkipIdempotency]`, which `AuthController` carries
    (AC-009-1 excludes `/auth/*`).
  - **Flow:**
    1. Validate the key (1–100 chars).
    2. `EnableBuffering`, then hash method + path + body (SHA-256).
    3. `TryBeginAsync`.
    4. Swap `Response.Body` for a buffer and run `next()`.
    5. If the executed context carries an exception, write it through the registered `IExceptionHandler`
       (`GlobalExceptionHandler`, the one error writer), so 4xx bodies are captured too.
    6. Store the response (2xx/4xx) or release the key (5xx), copy the buffer to the real body, and stamp `Idempotent-Replayed`
       on replays.
  - **Stored headers:** `Location`, `ETag`, `Content-Type`.
  - **User id** comes from `ICurrentUser`. Authorization middleware already ran, so it is always present.
- **Purge job:** `Infrastructure/Idempotency/PurgeExpiredIdempotencyRecordsJob`, hourly in `BackgroundJobsSetup` (AC-009-7),
  modelled on `PurgeExpiredRefreshTokensJob`.
- **Angular:** `core/http/idempotency-key.interceptor.ts` adds `Idempotency-Key: crypto.randomUUID()` to mutating `/api/` calls
  outside `/api/v1/auth/` when the header isn't already set.
  - It is registered **outside** `authInterceptor` (`[apiError, idempotencyKey, auth, apiResponse]`), so the 401-refresh retry
    resends the same key.
  - Double submits are already prevented by the disabled "Confirm" buttons.
- **Tests:**
  - integration `IdempotencyTests`: one test per AC-009-1…8, with expiry via `FakeTimeProvider` and the purge job;
  - **existing tests:** `ApiFactory` clients get a `DelegatingHandler` that adds a fresh key to mutating requests (mirrors the
    Angular interceptor), so the 103 existing integration tests keep working unchanged;
  - Vitest spec for the interceptor.
- **Docs:**
  - spec 009 Design → resource filter, text body, in-progress takeover;
  - ADR-0006 amendment;
  - SPEC §3 header + codes;
  - CLAUDE.md "every mutating endpoint requires an `Idempotency-Key`" (drop the "M3+" note);
  - `/new-feature` step 6;
  - backend, frontend and qa agents;
  - CHANGELOG, PROMPTS, backlog.

## S-32 — Rate limiting (NFR-6, ADR-0009)
- **`Api/Configuration/RateLimitingConfig.cs`** (extension block; `AddRateLimitingConfiguration()` + `UseRateLimitingConfiguration()`)
  with built-in `Microsoft.AspNetCore.RateLimiting`. Limits come from section `RateLimiting`:
  - **Global chained limiter:**
    - concurrency 200;
    - per partition (user id, else client IP): `reads` sliding window 300/min (6 segments) for GET/HEAD, and `writes` token
      bucket 30/min with burst 10 for other methods;
    - `/health` excluded.
  - **Named policy `auth`:** fixed window 5/min per IP, applied with `[EnableRateLimiting("auth")]` on login and refresh.
  - **`OnRejected`:** 429 `ApiResponse` error `RATE_LIMITED` (`ApiErrorCodes`) via `ApiErrorWriter`, `Retry-After` from lease metadata, and a counter
    `scheduler.rate_limit.rejected` on a `Meter("AppointmentScheduler")`. `ObservabilityConfig` adds `.AddMeter("AppointmentScheduler")`.
- **Pipeline:** `UseRateLimiter()` right after `UseAuthentication()`, so partitions see the user.
- **Tests:**
  - `ApiFactory` sets very high limits, so the suite never trips them;
  - new `RateLimitedFactory` (low limits) + `RateLimitTests`:
    - 6th login in a minute → 429 + `Retry-After` + envelope;
    - writes burst → 429;
    - another user is unaffected;
    - health is never limited.
- **Docs:** ADR-0009 status, NFR-6, README config table, architecture host-composition row, CLAUDE.md, devops agent, CHANGELOG,
  PROMPTS, backlog.

## S-33 — Npgsql retry (output caching skipped)
- **`AddPersistence`:** `UseNpgsql(cs, o => o.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: 2 s))` (NFR-7).
- **Required companion change:** EF retry strategies forbid user-started transactions. The four `BeginTransactionAsync` blocks are
  wrapped in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`, so each whole transaction retries as a unit:
  - `IdentityAuthService` (login reservation, refresh-family revoke);
  - `IdentityUserAdminService` (create, set roles).
- **Tests:** the existing auth/user-admin integration tests prove the transactions still work under the strategy, and a new test
  asserts the configured execution strategy is `NpgsqlRetryingExecutionStrategy`.
- **Backlog:** S-33 → "Versioning + Npgsql retry done; output caching won't do for now (nothing safely cacheable: availability
  must be live)". ADR-0008 resilience status updated.

## Verification (per story, then end to end)
- `dotnet build -warnaserror`; `dotnet test` (background); `cd web && npm test && npm run build`.
- Live API + browser:
  - book (request carries `Idempotency-Key`), replay the same request with curl → `Idempotent-Replayed: true` and one appointment;
  - 6 quick logins with curl → 429 `RATE_LIMITED` with `Retry-After`;
  - normal receptionist booking, cancel, reschedule and the doctor read-only view still work.
- Five steps (P, 0, S-30, S-32, S-33). After each I stop and ask "commit or leave in the working tree"; I never commit unasked.
