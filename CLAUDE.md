# Appointment Scheduler — Claude project memory

Clinic appointment scheduler: receptionists book patients with doctors in fixed slots **without double-booking**.
Simulates a production system (auth, idempotency, rate limiting, messaging, observability).

## Stack
- Backend: .NET 10, ASP.NET Core Web API (controllers), EF Core 10 + Npgsql, Clean Architecture (`src/`)
- Frontend: Angular 22 (standalone, signals, Angular Material) in `web/`
- Infra (Docker): PostgreSQL 17, RabbitMQ 4 (Wolverine; the API runs in degraded mode when the broker is down), Jaeger
  (OTLP :4317, UI :16686 — `ObservabilityConfig`). Background jobs: Hangfire inside the Api process
- Tests: **unit + architecture only** — xUnit (fakes, `FakeTimeProvider`), NetArchTest, and Vitest for Angular (plan 011).
  No integration tests, Testcontainers, `WebApplicationFactory`, Playwright or in-repo k6: never add or plan them. HTTP/DB
  behaviour is checked live (`curl` against the running API). Load testing happens outside the repo (scripts + k6 Studio).

## Commands
| Task | Command |
|---|---|
| Start infra | `docker compose up -d postgres rabbitmq jaeger` |
| Run API (http://localhost:5080, docs at `/scalar`) | `dotnet run --project src/AppointmentScheduler.Api` |
| All .NET tests (unit + architecture, no Docker) | `dotnet test` |
| Unit tests only | `dotnet test tests/AppointmentScheduler.UnitTests` |
| Add migration | `dotnet ef migrations add <Name> -p src/AppointmentScheduler.Infrastructure -s src/AppointmentScheduler.Api -o Persistence/Migrations` |
| Web dev server (http://localhost:4200, proxies `/api`) | `cd web && npm start` |
| Web tests / build | `cd web && npm test` / `npm run build` |

## Architecture rules (enforced by `tests/AppointmentScheduler.ArchitectureTests`)
- Dependency direction: **Domain ← Application ← Infrastructure ← Api** (the Api is the only host; background jobs and message handlers run in it). Domain references nothing.
- The Api references **Application and Infrastructure only, never Domain** (ArchitectureTests `Api_does_not_depend_on_domain`).
  Application hands the Api everything it needs: services, DTOs with Application enums (e.g. `AppointmentStatusDto`), and
  `ApplicationError.From(exception)` for expected failures. No Domain type appears in Api code or in a DTO.
- Feature folders in every layer: `Appointments/`, `Patients/`, `Doctors/`, `Clinics/`, `Auth/`. New feature = new folder + spec.
- Controllers (`Api/Controllers`, inherit `ApiControllerBase` → authenticated by default) are thin: bind → call an Application
  service → return. No business logic or EF queries in `Api`. Template in `.claude/skills/new-feature/SKILL.md` step 6.
  Every controller has `[ApiVersion(1.0)]` and `[Route("api/v{version:apiVersion}/<feature>")]` (ADR-0017; not inherited, so
  ArchitectureTests check it).
  Controllers hold **endpoints only**: no private/protected helper methods or constants (ArchitectureTests).
  - Logic and mapping go in the Application service, which keeps its own private helpers.
  - HTTP-only plumbing (cookies, headers) goes in an Api helper class next to its feature, e.g. `Api/Auth/RefreshTokenCookie`.
- Background jobs: Hangfire job classes in Infrastructure, registered in `BackgroundJobs/BackgroundJobsSetup.cs` (ADR-0014).
- `Program.cs` only composes: `builder.AddApiConfiguration().AddSecurityConfiguration().AddOpenApiConfiguration()…`, then the
  middleware order. Never register services inline in `Program.cs`:
  - a new host concern gets `Api/Configuration/<Concern>Config.cs`: a `public static class <Concern>Config` with a C# 14
    `extension(WebApplicationBuilder builder) { public WebApplicationBuilder Add<Concern>Configuration() … }` block
    (plus `extension(WebApplication app)` for its pipeline helpers);
  - layer services go in that layer's `DependencyInjection.cs` (see docs/architecture.md "Host composition").
- Business rules live in Domain (`Appointment`, `DaySchedule`); the DB exclusion constraint is the final guard (see SPEC.md).
- Data access (ADR-0016):
  - Application uses `IUnitOfWork`, never EF Core. Application has no EF package; ArchitectureTests enforce it.
  - **Services inject `IUnitOfWork` only** and reach every repository through it (`unitOfWork.Appointments.Add(...)`,
    `unitOfWork.Doctors.ExistsAsync(...)`). Never inject a repository: they are not registered in DI, and ArchitectureTests
    `Repositories_are_reached_only_through_the_unit_of_work` rejects a repository in any constructor.
  - Contracts sit next to their aggregate in Domain (`Domain/Appointments/IAppointmentRepository.cs`, …).
  - Implementations are `internal sealed` in `Infrastructure/Persistence/Repositories/`, created by `UnitOfWork` over its
    `AppDbContext`. A new repository = contract in Domain + implementation + a property on `IUnitOfWork` / `UnitOfWork`.
  - Repositories return entities, never `IQueryable`.
  - Repository methods only stage changes; the use case calls `unitOfWork.SaveChangesAsync` once (change + audit row together).
  - A new query is a named repository method, not inline LINQ in a service.

## Conventions
- Store time as UTC `DateTimeOffset`/`timestamptz` only. Clinic-local conversion happens server-side via the clinic's IANA zone.
- Get "now" from injected `TimeProvider` — never `DateTime.Now`/`UtcNow`.
- Responses (ADR-0015): every action returns `IActionResult` wrapping its DTO in `ApiResponse<T>` (`OkResponse(...)` =
  `ApiResponse<T>.Success`,
  `ApiResponse<T>.Created(...)`), and declares **every** status it can return with
  `[ProducesResponseType(typeof(ApiResponse<…>), StatusCodes.StatusNNN…)]` (401/500 come from `ApiControllerBase`). Explicit
  `[FromBody]`/`[FromRoute]`/`[FromQuery]`, `CancellationToken cancellationToken = default`. No `ModelState.IsValid` checks:
  `[ApiController]` already answers 400 `BAD_REQUEST` through `BadRequestResponse`.
- Errors: throw a `SchedulerException` subclass — `ValidationException` 400, `UnauthorizedException` 401,
  `ForbiddenException` 403, `NotFoundException` 404, `ConflictException` 409, `BusinessRuleException` 422, `LockedException`
  423 — with an `ErrorCodes.X` constant (never a string literal). Business codes go in `Domain/Common/ErrorCodes.cs`;
  transport codes that only the HTTP pipeline produces (BAD_REQUEST, ROUTE_NOT_FOUND, INTERNAL_ERROR, idempotency, rate
  limits) go in `Api/Errors/ApiErrorCodes.cs`. Either way the code goes under its feature section, UPPER_SNAKE (catalogue
  tests keep the two classes disjoint). Use a message safe to show users, and put diagnostics in `LogDetail` (ids only,
  never PII). `GlobalExceptionHandler` turns them into `ApiResponse` errors via `ApplicationError`; a transient database failure becomes 503
  `SERVICE_UNAVAILABLE`; anything else becomes a logged, generic 500 `INTERNAL_ERROR`. Never catch-and-return errors in controllers.
- Every **POST** requires an `Idempotency-Key` header, enforced by the global `IdempotencyFilter` (spec 009, plan 012); new POST
  endpoints get it for free. PUT/PATCH/DELETE are idempotent by design and need none, so design them that way (a PUT sets
  the full state, a DELETE of a deleted thing is still a success). `[SkipIdempotency]` exempts auth (controller) and any POST
  that only reads, e.g. a search with criteria in the body (put it on the action); never on a POST that changes data. Don't
  add idempotency logic in a service.
- Resilience (ADR-0008):
  - **Polly v8** only for calls to other services (SMTP, outbound HTTP) and for inbound rate limits: each is a named pipeline
    (`AddResiliencePipeline("<name>", …)`, resolved from `ResiliencePipelineProvider<string>`). No hand-written retry loops, no
    `Microsoft.AspNetCore.RateLimiting`.
  - **The database has no retry**: no `EnableRetryOnFailure`, no Polly DB pipeline. Consistency comes from atomicity (one
    `IUnitOfWork.SaveChangesAsync` per use case, explicit transactions where needed); a transient DB failure becomes 503
    `SERVICE_UNAVAILABLE` + `Retry-After` in `GlobalExceptionHandler`, and the client retries with the same `Idempotency-Key`.
  - Rate limits: `Api/Configuration/RateLimitingConfig.cs`; pick a non-default policy with
    `[RateLimitPolicy(RateLimitPolicies.X)]` on the action.
- Never log patient names, phone numbers or emails — log ids only.
- Fictional data only (seed in `Infrastructure/Persistence/Seed`).

## Workflow (spec-driven)
0. Plans: every approved plan is saved as `.claude/plans/NNN-<slug>.md` (next number, status header) and listed in
   `.claude/plans/README.md` before any code. Plan mode writes there directly (`plansDirectory` in `.claude/settings.json`);
   from another root, copy it in. When the plan changes during the work, edit that same file (dated "Changes" note) and tick
   its Progress line as steps are verified. Link the plan from the backlog story or spec it implements.
0b. Commits: never commit unasked. After each step, show what changed and ask whether to commit it or leave it in the working tree.
1. Spec first: `docs/specs/NNN-*.md` from `_template.md`, acceptance criteria `AC-NNN-x`.
2. Tests named after AC/BR IDs, written before or with the code: unit or architecture tests (Vitest for Angular). Behaviour
   only a real host or database shows is a live check recorded in the spec, never an integration test.
3. Default: implement directly in the session (use `/new-feature` as a checklist). The role-agent ceremony (`/sdlc`, docs/sdlc.md)
   is opt-in — only when the user asks for it.
4. Record meaningful prompts with `/log-prompt` (feeds PROMPTS.md).
5. Definition of Done: AC covered by passing tests · `/verify` green · SPEC.md / spec / README / CHANGELOG updated.
