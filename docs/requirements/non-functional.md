# Non-functional requirements

How well the system must do what [functional.md](functional.md) lists. NFR-1…NFR-11 are the measurable targets from
[spec 000](../specs/000-nfr.md); NFR-12…NFR-17 record qualities that are built and enforced but weren't listed there.
Each section has:
- the requirement and target;
- why it matters;
- how it is implemented (code and ADRs);
- how it is verified;
- its status.

**Status:** ✅ met · 🟡 partly met · 📋 planned. "Live" means checked against the running API (there are no integration
tests, [plan 011](../../.claude/plans/011-unit-and-architecture-tests-only.md)); "external load test" means k6 Studio or scripts run
outside the repository.

## Summary
| ID | Area | Status |
|---|---|---|
| [NFR-1](#nfr-1--correctness-no-double-booking) | Correctness: no double-booking, no lost update | ✅ |
| [NFR-2](#nfr-2--latency) | Latency | 📋 to measure |
| [NFR-3](#nfr-3--error-rate-under-load) | Error rate under load | 📋 to measure |
| [NFR-4](#nfr-4--security-authentication-and-authorization) | Security: authentication and authorization | ✅ |
| [NFR-5](#nfr-5--idempotency) | Idempotency | ✅ |
| [NFR-6](#nfr-6--abuse-protection-rate-limits) | Abuse protection (rate limits) | ✅ |
| [NFR-7](#nfr-7--resilience) | Resilience | 🟡 |
| [NFR-8](#nfr-8--observability) | Observability | 🟡 |
| [NFR-9](#nfr-9--privacy) | Privacy | ✅ |
| [NFR-10](#nfr-10--operability) | Operability | 🟡 |
| [NFR-11](#nfr-11--time-correctness) | Time correctness | ✅ |
| [NFR-12](#nfr-12--consistent-api-contract) | Consistent API contract | ✅ |
| [NFR-13](#nfr-13--api-versioning) | API versioning | ✅ |
| [NFR-14](#nfr-14--security-hardening) | Security hardening | ✅ |
| [NFR-15](#nfr-15--maintainability-enforced-architecture) | Maintainability (enforced architecture) | ✅ |
| [NFR-16](#nfr-16--portability-run-anywhere) | Portability (run anywhere) | ✅ |
| [NFR-17](#nfr-17--testability) | Testability | ✅ |

## NFR-1 — Correctness: no double-booking
- **Requirement:** zero overlapping *Scheduled* appointments per doctor and per patient, including under concurrent requests.
  Concurrent changes to the same appointment never overwrite each other silently.
- **Why:** a double-booked doctor or patient is the one failure the product exists to prevent.
- **How:** three layers ([ADR-0002](../adr/0002-postgres-exclusion-constraints.md)).
  - The UI offers only Free slots.
  - `AppointmentService` locks the doctor and patient (ordered advisory locks), then pre-checks overlaps and returns a
    precise 409. Concurrent bookings queue instead of deadlocking (SPEC §5).
  - PostgreSQL `EXCLUDE USING gist` constraints over `tstzrange(start, end, '[)')` (`ex_appointments_doctor_overlap`,
    `ex_appointments_patient_overlap`) are the guarantee. The violation (23P01) maps to the same 409 codes in `AppDbContext`.
  - Updates use the `xmin` concurrency token → 409 `CONCURRENCY_CONFLICT` ([ADR-0004](../adr/0004-concurrency-etag.md)).
- **Verified:** `AppointmentServiceTests` (pre-check rejects before anything is staged); live (parallel bookings of one slot →
  exactly one 201); external load test (`race-same-slot`).
- **Status:** ✅

## NFR-2 — Latency
- **Requirement:** `GET availability` p95 < 200 ms at 50 virtual users; `POST appointments` p95 < 400 ms at 20 virtual
  users (local Docker).
- **Why:** receptionists click through slots while a patient waits at the desk.
- **How:**
  - indexed queries (`(doctor_id, start_utc)`, `(patient_id, start_utc)`);
  - two appointment queries per availability request regardless of slot count;
  - `AsNoTracking` reads.
- **Verified:** external load test (k6 Studio). Not measured in the repository.
- **Status:** 📋 to measure

## NFR-3 — Error rate under load
- **Requirement:** < 1 % non-4xx failures under the NFR-2 load.
- **How:** the same as NFR-2. The rate limits (NFR-6) answer 429, not 5xx.
- **Verified:** external load test.
- **Status:** 📋 to measure

## NFR-4 — Security: authentication and authorization
- **Requirement:** every endpoint except login, refresh and health requires a JWT; role policies apply per endpoint group;
  an account locks after 5 failed logins.
- **Why:** patient schedules are personal data, and staff roles differ.
- **How** ([ADR-0005](../adr/0005-identity-jwt.md), [spec 004](../specs/004-auth.md)):
  - `ApiControllerBase` is authenticated by default;
  - `Api/Auth/Policies.cs` holds the role matrix;
  - `ScheduleAccess` enforces BR-7 (a Doctor sees only their own data, [ADR-0012](../adr/0012-br7-resource-check-in-application.md));
  - ASP.NET Identity stores PBKDF2 hashes;
  - access tokens last 15 minutes, with refresh-token rotation and reuse detection;
  - the lockout uses an atomic attempt reservation.
- **Verified:** `ScheduleAccessTests`, `AuthValidatorTests`, architecture tests (controllers inherit the base); live; security
  review.
- **Status:** ✅

## NFR-5 — Idempotency
- **Requirement:** every POST needs an `Idempotency-Key`; a retried request returns the original response and creates no
  duplicate. PUT, PATCH and DELETE are idempotent by design.
- **Why:** networks drop responses, and a retried booking must not book twice.
- **How:**
  - `IdempotencyFilter`, a global MVC resource filter;
  - the `idempotency_records` table with a unique `(user_id, key)` index as the lock;
  - a 24 h time-to-live and an hourly purge ([spec 009](../specs/009-idempotency.md), [ADR-0006](../adr/0006-idempotency-records.md),
    plan 012).
- **Verified:** `IdempotencyScopeTests`; live (replay, reuse 422, missing key 400); the Angular interceptor spec.
- **Status:** ✅

## NFR-6 — Abuse protection: rate limits
- **Requirement:** login 5/min per IP; writes 30/min per user (burst 10); reads 300/min per user → 429 `RATE_LIMITED` +
  `Retry-After`.
- **Why:** brute-force logins, and one busy client starving others.
- **How:**
  - Polly v8 rate-limit pipelines `rate-limit:global/reads/writes/auth` (`Api/Configuration/RateLimitingConfig.cs`,
    `PollyRateLimitingMiddleware`), partitioned by user id, else client IP ([ADR-0009](../adr/0009-rate-limiting.md));
  - rejections are counted in `scheduler.rate_limit.rejected`.
- **Verified:** `RateLimitPartitionKeyTests`; live (the 6th login in a minute → 429).
- **Limit:** behind the Docker nginx every browser shares one IP until `ForwardedHeaders` is added (S-61).
- **Status:** ✅

## NFR-7 — Resilience
- **Requirement:**
  - the database has no retry: the unit of work is atomic, and a transient failure → 503 `SERVICE_UNAVAILABLE` +
    `Retry-After`;
  - calls to other services (SMTP) use timeout + retry + circuit breaker;
  - failed messages are retried, then dead-lettered.
- **Why:** an outage must never leave half-saved data, and a dead dependency must fail fast.
- **How** ([ADR-0008](../adr/0008-resilience-polly.md)):
  - one `SaveChangesAsync` per use case;
  - `GlobalExceptionHandler` maps `DbException.IsTransient` to 503;
  - the idempotency filter releases keys on 5xx, so the client retry is safe;
  - Wolverine runs a degraded mode while RabbitMQ is down ([ADR-0007](../adr/0007-wolverine-outbox-rabbitmq.md)).
- **Verified:** live (stop Postgres → 503; start it → 200).
- **Status:** 🟡 — the database part is built. The SMTP pipeline and the message dead-lettering arrive with notifications
  (spec 008).

## NFR-8 — Observability
- **Requirement:** every request is traced end to end in Jaeger; logs carry `TraceId`; metrics are exported.
- **Why:** to find a slow or failing request without guessing.
- **How** ([ADR-0010](../adr/0010-observability-otel-jaeger.md), `Api/Configuration/ObservabilityConfig.cs`):
  - OpenTelemetry traces (ASP.NET Core, HttpClient, EF Core, Wolverine) go over OTLP to Jaeger;
  - spans are tagged `enduser.id` (id only);
  - metrics (ASP.NET Core, HttpClient, `scheduler.*` counters) go to OTLP when `Observability:MetricsEndpoint` is set.
- **Verified:** live (Jaeger shows `appointment-scheduler-api` spans for each request).
- **Status:** 🟡 — traces and metrics export are built. JSON logs (Serilog) and a `/metrics` endpoint are still to do (S-60).

## NFR-9 — Privacy
- **Requirement:** no patient PII in logs or traces; fictional seed data only.
- **How:**
  - exceptions carry ids-only `LogDetail`;
  - request logs record method and path only, never the query or body;
  - audit rows hold ids;
  - the database-outage log keeps the exception type, not the provider message;
  - the seed is fictional (`DbSeeder`).
- **Verified:** code review; log grep.
- **Status:** ✅

## NFR-10 — Operability
- **Requirement:** `/health/live` and `/health/ready` (database + broker); `docker compose up` brings the whole system up
  from a clean clone.
- **How:**
  - `MapHealthChecks("/health/live")`;
  - Dockerfiles for the API and web, and compose services `api` and `web` with health-gated `depends_on` (plan 014);
  - migrate-on-start in the Development demo stack.
- **Verified:** the README Quick start, run from a clean state; `/verify`.
- **Status:** 🟡 — liveness and the full compose stack are built. `/health/ready`, a separate migrator and `operations.md`
  are still to do (S-60, S-61).

## NFR-11 — Time correctness
- **Requirement:** every instant is stored in UTC; clinic-local presentation is computed server-side from the clinic's IANA
  zone; DST days are handled.
- **How** ([ADR-0003](../adr/0003-time-zones.md)):
  - `timestamptz` only;
  - `DaySchedule` builds slots in local time and converts them to UTC (DST gap times are skipped);
  - the API returns `startUtc`, `localStart` and `timeZone`;
  - "now" comes from an injected `TimeProvider`.
- **Verified:** `DayScheduleTests` (DST case, local-day window).
- **Status:** ✅

## NFR-12 — Consistent API contract
- **Requirement:** every response, success or error and including framework-made ones (401/403/404/405/415/429/500/503),
  has one `ApiResponse` shape with a stable UPPER_SNAKE `errorCode` and a `traceId`. Every action declares every status it
  can return.
- **Why:** clients branch on codes, never on message text.
- **How** ([ADR-0015](../adr/0015-api-response-envelope.md)):
  - `Api/Contracts/ApiResponse.cs`, `ApiErrorWriter`, `GlobalExceptionHandler`, `ErrorStatusMapping`;
  - business codes in `Domain/Common/ErrorCodes.cs`, transport codes in `Api/Errors/ApiErrorCodes.cs`;
  - `[ProducesResponseType]` on every action → OpenAPI and Scalar.
- **Verified:** `ErrorCodesTests` / `ErrorCodeCatalogTests` (unique, UPPER_SNAKE, no clashes); live.
- **Status:** ✅

## NFR-13 — API versioning
- **Requirement:** the version is in the URL (`/api/v1/…`); a mismatched header or query version → 400; an unknown URL
  version → 404 `UNSUPPORTED_API_VERSION`.
- **How:** Asp.Versioning (`Api/Configuration/ApiVersioningConfig.cs`, [ADR-0017](../adr/0017-api-versioning.md)).
- **Verified:** architecture test `Controllers_declare_their_api_version`; live.
- **Status:** ✅

## NFR-14 — Security hardening
- **Requirement:**
  - security headers on every response;
  - HSTS outside Development;
  - CORS restricted to configured origins;
  - a refresh cookie that JavaScript can't read;
  - a password policy;
  - no secrets in the repository.
- **How:**
  - `Api/Security/SecurityHeaders.cs`: `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, and a CSP on
    `/api/v1`;
  - `SecurityConfig` (HSTS, CORS from `Cors:AllowedOrigins`);
  - the cookie is HttpOnly, Secure, SameSite=Strict, with path `/api/v1/auth`;
  - `PasswordPolicy`;
  - the JWT key comes from configuration, with a dev-only key in Development only.
- **Verified:** live (response headers); security review.
- **Status:** ✅

## NFR-15 — Maintainability: enforced architecture
- **Requirement:**
  - Clean Architecture dependency rules (Domain ← Application ← Infrastructure ← Api; the Api never uses Domain types);
  - Application has no EF Core;
  - repositories are reached only through the unit of work and inherit the generic base;
  - controllers hold endpoints only.
- **How:**
  - [ADR-0001](../adr/0001-clean-architecture.md), [ADR-0016](../adr/0016-repository-unit-of-work.md);
  - feature folders in every layer;
  - host configuration as `*Config` extension blocks.
- **Verified:** `ArchitectureTests` (16 rules, e.g. `Api_does_not_depend_on_domain`,
  `Repositories_are_reached_only_through_the_unit_of_work`, `Repository_implementations_inherit_the_generic_base`). The
  build fails on any analyzer warning.
- **Status:** ✅

## NFR-16 — Portability: run anywhere
- **Requirement:** anyone with Docker can run the whole system with one command, with no .NET or Node install.
- **How:**
  - `src/AppointmentScheduler.Api/Dockerfile` (multi-stage, Debian runtime with tzdata);
  - `web/Dockerfile` + `web/nginx.conf` (same-origin proxy for `/api`);
  - compose services `api` and `web`.
- **Verified:** the README Quick start (`docker compose up --build -d`, sign in, book, cancel).
- **Status:** ✅ (local demo stack; production configuration is still to do)

## NFR-17 — Testability
- **Requirement:** rules and use cases are testable without a database or host. The suite is unit + architecture tests
  (+ Vitest) and runs without Docker.
- **How:**
  - `TimeProvider` injected everywhere;
  - repositories and the unit of work behind interfaces (fakes in `AppointmentServiceTests`);
  - validators separate from services;
  - pure helpers (`IdempotencyFilter.Applies`, `RateLimitPartitionKey`) are unit-testable.
- **Verified:** `dotnet test`, `npm test`.
- **Status:** ✅
