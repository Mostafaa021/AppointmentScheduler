# Changelog

Format: [Keep a Changelog](https://keepachangelog.com). Versions follow milestones.

## [Unreleased]
### Removed
- Angular unit tests (plan 016, user decision): the 10 `*.spec.ts` files, `tsconfig.spec.json`, the `test` target and
  script, and the `vitest` + `jsdom` dev dependencies. CI builds the web app only; UI behaviour is checked live in the browser.
  Claude agents, skills and docs no longer plan or run Angular tests.
- Integration tests, Testcontainers and `WebApplicationFactory` (plan 011, user decision): only unit and architecture tests
  (plus Vitest) remain, and `dotnet test` no longer needs Docker. The pure `RateLimitPartitionKeyTests` moved to UnitTests.
  Planned Playwright e2e and the in-repo k6 suite are dropped (`/load-test` skill removed); load testing happens outside
  the repo (scripts + k6 Studio). Claude agents, skills and docs now forbid planning them.
### Changed
- Local PostgreSQL password is now `P@ssword!` (`appsettings.json`, compose defaults, `.env.example`, README). An existing
  data volume keeps its old password until changed with `ALTER USER` (README troubleshooting). README gains a pgAdmin guide.
- Concurrent bookings of one slot no longer deadlock (plan 015, SPEC §5, ADR-0002 amended). Measured before: 1×201 and
  7×503 after up to 7 s (PostgreSQL 40P01 on the exclusion constraint). Book and reschedule now lock the doctor, then the
  patient (`pg_advisory_xact_lock`), then pre-check, inside `IUnitOfWork.ExecuteInTransactionAsync`. Measured after:
  1×201 and 7×409 `DOCTOR_CONFLICT` in about 0.4 s. A remaining 40P01 maps to 409 `CONCURRENCY_CONFLICT`, not 503.
  SPEC §4–§6 rewritten as the task-07 answer, with rejected alternatives and a copy-paste race check.
- Every repository inherits a generic base (plan 013, ADR-0016 amended): `IRepository<TEntity, TKey>` (Domain) with `Add`,
  `GetByIdAsync`, `GetByIdReadOnlyAsync`, `ExistsAsync`, implemented once by `Repository<TEntity, TKey>` (Infrastructure);
  entities implement `IEntity<TKey>`. No Remove or list-everything; no `IQueryable`. ArchitectureTests enforce that
  contracts extend it and implementations inherit it. No behaviour change.
- `Idempotency-Key` is required on **POST only** (spec 009 AC-009-1, ADR-0006 amended): PUT, PATCH and DELETE are idempotent
  by design and need none (today this frees `PUT /users/{id}/roles`). `[SkipIdempotency]` is documented for single actions
  too: a POST that only reads, such as a search. One scope check (`IdempotencyFilter.Applies`) drives the filter and the
  OpenAPI header, and is unit-tested. The Angular interceptor keys POSTs only.
- Repositories are reached only through the unit of work (plan 010, ADR-0016 amended): `IUnitOfWork` exposes `Appointments`,
  `Doctors`, `Patients`, `AuditRecords`; services inject `IUnitOfWork` alone, repositories are no longer registered in DI, and
  ArchitectureTests reject a repository in any constructor. No behaviour change.
### Added
- Optional pgAdmin web UI in Docker Compose (plan 017): `docker compose --profile tools up -d pgadmin` →
  http://localhost:5050 (127.0.0.1 only, desktop mode, no login). The `scheduler` database is pre-registered
  (`tools/pgadmin/servers.json`, `gssencmode=disable`, password written to a 0600 pgpass at start), so it opens connected.
  It works around pgAdmin 9.18's Windows crash ("access violation writing 0x0…"). The README also documents the desktop fix.
- `GET /api/v1/clinic` (plan 015): the clinic's zone, hours and **clinic-local today**. The schedule pages open on it and
  **Today** returns to it, instead of the browser's date (a browser in New York at 20:00 is already on London's tomorrow).
- Requirements and architecture documentation (plan 014 step B): `docs/requirements/functional.md` (business rules + every
  functional requirement per feature with status, API, code and verification) and `non-functional.md` (NFR-1…17: why, how,
  verified, status); `docs/architecture.md` refreshed (real containers, Docker deployment, data model ER diagram, pipeline)
  plus one Mermaid page per feature in `docs/architecture/` (13 pages, 40 diagrams, built and planned features).
- Run anywhere with Docker (plan 014, S-61 part): `src/AppointmentScheduler.Api/Dockerfile`, `web/Dockerfile` + `web/nginx.conf`
  (serves the Angular build, proxies `/api/` same-origin) and compose services `api` + `web`, so `docker compose up --build -d`
  runs the whole system on http://localhost:8080 with only Docker installed (local demo stack, Development mode). README
  rewritten for newcomers: how it works, quick start, URLs, everyday commands, local development, troubleshooting.
- 503 `SERVICE_UNAVAILABLE` + `Retry-After: 5` when the database fails transiently (S-33, ADR-0008 revised): no server-side
  retry and no Polly for the database. The atomic unit of work guarantees nothing is half-saved, and the client retries with
  the same `Idempotency-Key`. Polly stays for other services (SMTP, outbound HTTP) and the rate limits.
- Rate limiting with Polly v8 (S-32, ADR-0009 revised, ADR-0008 "Polly only"): named pipelines `rate-limit:global` (200
  concurrent), `reads` (300/min sliding), `writes` (burst 10, 30/min token bucket) per user or IP, and `auth` (5/min per IP on
  login/refresh). Rejections → 429 `RATE_LIMITED` + `Retry-After`, counted in `scheduler.rate_limit.rejected`.
- Idempotent writes (spec 009, S-30): every POST/PUT/PATCH/DELETE except auth needs `Idempotency-Key`; replay with
  `Idempotent-Replayed: true`, 422 on reuse, 409 + `Retry-After` while in progress, 5xx not stored. `idempotency_records` table
  (unique `(user_id, key)` as the lock), hourly purge job, Angular `idempotencyKeyInterceptor`, header documented in OpenAPI.
- `.claude/plans/`: every approved plan saved as `NNN-<slug>.md` with a status header and an index (`plansDirectory` set in
  `.claude/settings.json`); plans 001–008 backfilled from history, 009 = Sprint 3.
- M2 security: ASP.NET Identity + JWT (15 min) with rotating refresh-token cookie and reuse detection; roles Admin /
  Receptionist / Doctor; Doctor reads only own schedule; FluentValidation; security headers; audit records; Angular login,
  silent refresh, guards and read-only doctor view. Demo users in Development.
- Hangfire background jobs inside the API (PostgreSQL storage, dev dashboard) with an hourly expired-refresh-token purge.
- Global exception handler: unexpected errors return a generic 500 `internal_error` and are logged with the trace id.
### Changed
- The Api references Application and Infrastructure only, never Domain (architecture test). Expected failures reach it as
  `ApplicationError`. Transport error codes moved to `Api/Errors/ApiErrorCodes` (values unchanged).
  `AppointmentDto.Status` is the Application enum `AppointmentStatusDto` (same JSON).
- API moved from Minimal APIs to controllers (routes, status codes and error codes unchanged).
- **Breaking:** every response body is now an `ApiResponse<T>` envelope (`isSuccess`, `statusCode`, `message`, `data`,
  `errorDetails` (Development only), `timestamp` + `errorCode`, `errors`, `traceId`; template factories `Success` / `Error` /
  `NotFound` / `BadRequest` / `Created` / `NoContent`) instead of bare DTOs + RFC 7807 ProblemDetails; error codes are UPPER_SNAKE constants in
  `ErrorCodes` (`doctor_conflict` → `DOCTOR_CONFLICT`). Unknown routes / wrong methods answer `ROUTE_NOT_FOUND` /
  `METHOD_NOT_ALLOWED`. Controllers declare every status with `[ProducesResponseType]`. Exceptions carry their own status
  and a log-only `LogDetail` (ADR-0015).
- `Program.cs` is a composition root only: `AddApiConfiguration` / `AddSecurityConfiguration` / `AddOpenApiConfiguration`
  written as C# 14 `extension(WebApplicationBuilder)` blocks in `Api/Configuration/*Config.cs`, `ApplyMigrationsAsync()`, health checks via
  `MapHealthChecks("/health/live")`; `AddInfrastructure` split into `AddPersistence`, `AddIdentityAndJwt`, `AddBackgroundJobs`.
- Data access through repositories (`IAppointmentRepository`, `IDoctorRepository`, `IPatientRepository`,
  `IAuditRecordRepository`) and `IUnitOfWork` (ADR-0016); `IAppDbContext` removed and Application no longer references EF
  Core (enforced by ArchitectureTests). First Application unit tests with fakes (`AppointmentServiceTests`).
- Controllers hold endpoints only: refresh-cookie handling moved from `AuthController` to `Api/Auth/RefreshTokenCookie`;
  `ControllerConventionTests` forbid private helpers and constants in controllers.
- Framework-made errors (404/405 status pages, 415 from MVC, Asp.Versioning) all written as `ApiResponse` through
  `ApiResponseProblemDetailsWriter` / `ApiResponseClientErrorFactory`; new codes `UNSUPPORTED_MEDIA_TYPE`, `UNSUPPORTED_API_VERSION`.
- API versioning (Asp.Versioning, ADR-0017): routes `api/v{version:apiVersion}/…`, `[ApiVersion(1.0)]` per controller,
  `api-supported-versions` header, `X-Api-Version` / `api-version` accepted; unknown versions → `UNSUPPORTED_API_VERSION`.
- OpenTelemetry tracing → Jaeger over OTLP (ASP.NET Core, HttpClient, EF Core, Wolverine source; `enduser.id` tag) and
  optional OTLP metrics, via `ObservabilityConfig` (ADR-0010 status).
- Messaging wiring (ADR-0007 status): Wolverine in the Api host with a PostgreSQL message store (schema `wolverine`), RabbitMQ
  transport when the startup probe succeeds (topic exchange `appointment-scheduler.events`), degraded mode +
  `RabbitMqRecoveryService` otherwise. No events published yet (M5).
### Fixed
- RabbitMQ dashboard login: local Docker now uses the default `guest` / `guest` (was `scheduler` / `scheduler_dev_pw`);
  `appsettings` and `.env.example` updated to match.
- Integration-test host crash ("Test Run Aborted") at shutdown: `ApiFactory` now disposes the host before the database.
### Removed
- `AppointmentScheduler.Worker` project (the API is the single host) and Mailpit from docker-compose.
### Security
- `Newtonsoft.Json` pinned to 13.0.4 (Hangfire.Core referenced vulnerable 11.0.1, GHSA-5crp-9r3c-p9vr).

## [v1-challenge] — 2026-10-19
### Added
- Availability per doctor and clinic-local date: 30-minute slots 09:00–17:00 (Europe/London) as Free / Booked / Past / PatientBusy.
- Book, cancel (soft, with reason) and reschedule appointments; day schedule including cancelled ones.
- Conflict rules: doctor and patient overlaps rejected by PostgreSQL exclusion constraints (+ friendly pre-check), no past
  bookings, slot grid validation, only future scheduled appointments can change; `xmin` optimistic concurrency.
- RFC 7807 ProblemDetails with stable `code` values; OpenAPI + Scalar UI.
- Fictional seed data (4 doctors, 12 patients, appointments today/tomorrow).
- Angular 22 schedule page with booking (review step), reschedule and cancel dialogs.
- Tests: 20 unit, 3 architecture, 24 integration (Testcontainers, parallel booking race), 11 Angular.

### Foundation (M0)
- Clean Architecture solution, central package management, Docker infra (PostgreSQL, RabbitMQ, Mailpit, Jaeger),
  specs 000–009, ADRs 0001–0010, SDLC role agents and project skills, CI workflow.
