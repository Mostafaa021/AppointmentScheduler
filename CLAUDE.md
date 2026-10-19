# Appointment Scheduler — Claude project memory

Clinic appointment scheduler: receptionists book patients with doctors in fixed slots **without double-booking**.
Simulates a production system (auth, idempotency, rate limiting, messaging, observability, load tests).

## Stack
- Backend: .NET 10, ASP.NET Core Web API (controllers), EF Core 10 + Npgsql, Clean Architecture (`src/`)
- Frontend: Angular 22 (standalone, signals, Angular Material) in `web/`
- Infra (Docker): PostgreSQL 17, RabbitMQ 4 (Wolverine, M5), Jaeger. Background jobs: Hangfire inside the Api process
- Tests: xUnit, Testcontainers, NetArchTest, Vitest, Playwright, k6 (`perf/k6`)

## Commands
| Task | Command |
|---|---|
| Start infra | `docker compose up -d postgres rabbitmq jaeger` |
| Run API (http://localhost:5080, docs at `/scalar`) | `dotnet run --project src/AppointmentScheduler.Api` |
| All .NET tests (needs Docker) | `dotnet test` |
| Unit tests only | `dotnet test tests/AppointmentScheduler.UnitTests` |
| Add migration | `dotnet ef migrations add <Name> -p src/AppointmentScheduler.Infrastructure -s src/AppointmentScheduler.Api -o Persistence/Migrations` |
| Web dev server (http://localhost:4200, proxies `/api`) | `cd web && npm start` |
| Web tests / build | `cd web && npm test` / `npm run build` |
| Load test | `docker compose --profile perf run --rm k6 run /scripts/<script>.js` |

## Architecture rules (enforced by `tests/AppointmentScheduler.ArchitectureTests`)
- Dependency direction: **Domain ← Application ← Infrastructure ← Api** (the Api is the only host; background jobs and message handlers run in it). Domain references nothing.
- Feature folders in every layer: `Appointments/`, `Patients/`, `Doctors/`, `Clinics/`, `Auth/`. New feature = new folder + spec.
- Controllers (`Api/Controllers`, inherit `ApiControllerBase` → authenticated by default) are thin: bind → call an Application
  service → return. No business logic or EF queries in `Api`.
- Background jobs: Hangfire job classes in Infrastructure, registered in `BackgroundJobs/BackgroundJobsSetup.cs` (ADR-0014).
- Business rules live in Domain (`Appointment`, `DaySchedule`); the DB exclusion constraint is the final guard (see SPEC.md).

## Conventions
- Store time as UTC `DateTimeOffset`/`timestamptz` only. Clinic-local conversion happens server-side via the clinic's IANA zone.
- Get "now" from injected `TimeProvider` — never `DateTime.Now`/`UtcNow`.
- Errors: throw domain exceptions (`ValidationException` 400, `UnauthorizedException` 401, `ForbiddenException` 403,
  `NotFoundException` 404, `ConflictException` 409, `BusinessRuleException` 422, `LockedException` 423).
  `GlobalExceptionHandler` maps them to RFC 7807 ProblemDetails with a stable `code`; anything else becomes a logged, generic
  500 `internal_error`. Never catch-and-return errors in controllers.
- Every mutating endpoint requires an `Idempotency-Key` header (M3+).
- Never log patient names, phone numbers or emails — log ids only.
- Fictional data only (seed in `Infrastructure/Persistence/Seed`).

## Workflow (spec-driven)
1. Spec first: `docs/specs/NNN-*.md` from `_template.md`, acceptance criteria `AC-NNN-x`.
2. Tests named after AC/BR IDs, written before or with the code.
3. Default: implement directly in the session (use `/new-feature` as a checklist). The role-agent ceremony (`/sdlc`, docs/sdlc.md)
   is opt-in — only when the user asks for it.
4. Record meaningful prompts with `/log-prompt` (feeds PROMPTS.md).
5. Definition of Done: AC covered by passing tests · `/verify` green · SPEC.md / spec / README / CHANGELOG updated.
