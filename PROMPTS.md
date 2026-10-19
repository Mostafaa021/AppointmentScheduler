# PROMPTS

Meaningful prompts used while building this project with Claude Code, and what I did with the output.
Format per entry: **Phase · Prompt (abridged) · Outcome: Accepted / Changed / Rejected — why.**

## Planning

### P-01 · Turn the brief into a spec-driven plan
> "Read the PDF task in this folder. Take all requirements into consideration, make it clear and scalable for adding new
> features, and plan it spec-driven before we start, including all Claude files, skills and documentation."

- **Accepted:** three-layer conflict checking (UI hint → API pre-check → Postgres exclusion constraint), UTC storage with
  clinic-zone slot generation, `TimeProvider` for testable "now", feature folders.
- **Changed:** first draft proposed SQLite + a single API project; I chose PostgreSQL (enables range exclusion constraints) and
  Clean Architecture (4 projects + Worker) to show layering.
- **Rejected:** a unique index on `(doctor_id, start_time)` as the only DB guard — it misses partial overlaps once durations vary.

### P-02 · Expand to a production simulation
> "Add as in-scope all non-functional requirements: authentication and authorization, rate limiting, resilience patterns (retry,
> circuit breaker…), unit tests, k6 scripts, a global idempotency middleware/filter with an idempotency records table, RabbitMQ
> with Wolverine if needed… treat this as a simulation of production."

- **Accepted:** milestone plan where M1 = exact challenge (tagged `v1-challenge`) and M2–M7 add NFRs; Identity + JWT with
  rotating refresh tokens; endpoint filter (not middleware) for idempotency so it applies per route group; Wolverine EF Core outbox.
- **Changed:** proposed Aspire dashboard for observability → switched to OpenTelemetry + Jaeger as requested.
- **Rejected:** MassTransit (licensing) and a Redis distributed lock for idempotency — the unique index on `(user_id, key)` is the lock.

### P-03 · SDLC role agents
> "Create AI agents with specific instructions for each role of the SDLC so the whole life cycle works in ceremony."

- **Accepted:** 10 role agents with explicit inputs/outputs/gates, hand-off through repo files, `/sdlc` skill with a `--fast`
  path for small changes (used for the interview live change).
- **Changed:** merged a separate "spec-reviewer" into `qa-engineer` + `code-reviewer` to avoid overlapping responsibilities.

## Coding

### P-04 · Domain rules test-first
> "Implement M1 domain from specs 001–003: an Appointment aggregate enforcing BR-3/4/5 and slot generation in the clinic's
> time zone. Write xUnit tests named after the AC IDs first, including a DST day."

- **Accepted:** `Appointment.Schedule/Cancel/Reschedule` taking an injected "now"; slot validity checked by comparing against the
  generated slots of that local day (one rule handles grid, hours and DST); `Guid.CreateVersion7()` ids.
- **Changed:** planned name `AvailabilityPolicy` became `DaySchedule` (it is a value describing a day, not a policy object);
  reason validation moved into the aggregate (400 `validation_failed`) instead of duplicating it in the endpoint.
- **Rejected:** a separate `SlotTime` value object and a repository per entity — no behavior to justify them yet.

### P-05 · Database guarantee for overlaps
> "Add the initial EF Core migration and append PostgreSQL exclusion constraints for doctor and patient overlaps
> (btree_gist, tstzrange '[)', only Scheduled rows). Translate the violation into the same 409 codes as the pre-check."

- **Accepted:** raw SQL in the migration, constraint-name → `doctor_conflict`/`patient_conflict` mapping in `AppDbContext.SaveChangesAsync`,
  `xmin` as the concurrency token.
- **Changed:** error factories moved to a shared `AppointmentConflicts` class so the pre-check and the DB translation can't drift.
- **Rejected:** wrapping booking in a SERIALIZABLE transaction — redundant with the constraint and adds retry handling.

### P-06 · Angular schedule feature
> "Build the Angular schedule page: doctor select + date picker, slot grid (Free/Booked/Past/PatientBusy), day list with
> cancel/reschedule, and a book dialog with patient autocomplete, reason and a review step. Never convert time zones in the browser."

- **Accepted:** presentational `SlotGrid` reused by the page and the reschedule dialog; `localStart` from the API for display,
  `startUtc` posted back; a `toIsoDate` helper using local getters so the picked day doesn't shift.
- **Changed:** on a 409 `doctor_conflict` the book dialog now closes and refreshes the grid instead of only showing an error.
- **Rejected:** a NgRx store — three signals per page are enough at this size.

## Debugging / refactoring

### P-07 · 500 instead of 400 for a malformed date
> "AC_001_7 fails: `?date=not-a-date` returns 500, expected 400. Find the root cause."

- **Accepted:** in Development minimal APIs throw `BadHttpRequestException` for binding failures, and `UseExceptionHandler`
  turned it into a 500. The exception handler now maps it to its own status code as ProblemDetails `bad_request`.
- **Rejected:** first suggestion was to bind `date` as `string` and parse manually — hides the real problem and repeats per endpoint.

### P-08 · Is the race test really testing the database?
> "The parallel-booking test passes, but could the application pre-check alone be winning it? Prove the constraint works."

- **Accepted:** added `ExclusionConstraintTests` that save overlapping appointments directly through `AppDbContext`, bypassing the
  service — the DB still rejects them with the right codes, and allows back-to-back and cancelled overlaps.

### P-09 · Keep the Application layer provider-agnostic
> "Patient search: case-insensitive contains on name or exact MRN."

- **Rejected:** the generated `EF.Functions.ILike` — it is Npgsql-specific and would make Application depend on the provider
  (the ArchitectureTests forbid `Npgsql` there). Used `ToLower().Contains()` which EF translates to `lower(...) LIKE`.
- **Changed:** a `Directory` namespace shadowed `System.IO.Directory` under implicit usings → renamed to `Lookups`.

### P-10 · Running M2 through the role-agent ceremony — and stopping it
> "Build M2 through the /sdlc role-agent ceremony, pausing for my approval after the architect step." … later: "Stop the
> ceremony; let's focus on coding."

- **Accepted:** product-owner and architect output (26 AC, ADR-0011/0012/0013) and the built code — verified independently
  (142 .NET + 32 web tests, browser login as receptionist and doctor) before committing.
- **Changed:** at the approval checkpoint I overrode the architect's hand-written validation in favour of FluentValidation
  (ADR-0013 revised).
- **Rejected (for now):** the remaining QA / security / code-review gates — backlog marks M2 "built, review pending".

### P-11 · Controllers instead of Minimal APIs
> "Use controllers instead of minimal APIs, and a global exception handler."

- **Accepted:** `ApiControllerBase` (`[ApiController]` + `[Authorize]` by default), one controller per feature, same routes,
  status codes and error codes, so all 142 existing tests passed after only one test change.
- **Changed (debugging):** returning `TypedResults` from controllers serialised enums as numbers — `IResult` uses the
  Minimal-API JSON options, not MVC's. Switched to MVC results (`Ok`, `CreatedAtRoute`, `NoContent`). The auth sweep test
  matched `/api/v1`, but controller route templates have no leading slash — normalised in the test.
- **Changed:** MVC's implicit `[Required]` on non-nullable members would have answered 400 before FluentValidation ran,
  breaking the `validation_failed` contract — disabled with `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes`.
- **Rejected:** a custom 499 branch in the global handler for client aborts — the test proved `ExceptionHandlerMiddleware`
  already handles aborts before any `IExceptionHandler` runs, so the branch was dead code.

### P-12 · No Worker project, Hangfire instead, no Mailpit
> "Why a separate Worker dll? What is Mailpit for? Use Hangfire inside our project instead of the worker; remove Mailpit."

- **Accepted:** Hangfire server inside the Api with PostgreSQL storage, one real hourly job (purge expired refresh tokens)
  with an integration test; Wolverine + RabbitMQ kept for domain events (ADR-0014 explains the split).
- **Changed:** the build failed with NU1903 — Hangfire.Core pulls `Newtonsoft.Json` 11.0.1 (known vulnerability). Fixed by
  central transitive pinning to 13.0.4 instead of suppressing the warning.

### P-13 · House-style responses, exceptions and error codes
> "Build endpoints with a structure like this [`ProducesResponseType` per status, `ApiResponse<T>.Created(...)`], return a
> generic API response and a status code for each one; build exceptions like `PartnerWalletException` (ErrorCode, StatusCode,
> log-only LogDetail) and error codes like `ErrorCodes`; update the Claude files and workflows to match."

- **Accepted:** `ApiResponse<T>` for every body, `SchedulerException` with status on the exception, `ErrorCodes` constants,
  `[ProducesResponseType]` on every action, `LogDetail` used for constraint names / token family / BR-7 ids (ADR-0015).
- **Changed:** business rules stay **422** (the example used 400) so form errors and rule rejections differ by status; codes
  became UPPER_SNAKE. No `if (!ModelState.IsValid)` in actions — `[ApiController]` answers first, so it would be dead code; the
  model-state factory returns the same envelope instead. Logout keeps 204 without a body.
- **Changed (debugging):** removing `AddProblemDetails()` made `UseExceptionHandler()` throw at startup (all 91 integration
  tests failed) — it needs a fallback even though `GlobalExceptionHandler` always handles; gave it a no-op `ExceptionHandler`.
  A global `WhenWritingNull` would have dropped `doctorId: null` from DTOs the web app reads, so null-skipping is scoped to
  the envelope's own properties.
- **Rejected:** action-name routes like `CreateDelegatedWallet` — the REST routes stay unchanged.

### P-14 · Program.cs as a composition root
> "For Program.cs follow this template [`builder.AddApiConfiguration().AddMessagingConfiguration()…`, `await
> app.ApplyMigrationsAsync()`, Hangfire section] — instead of a lot of service injection in Program.cs use extension methods."

- **Accepted:** `Add<Concern>Configuration()` extensions on `WebApplicationBuilder` in `Api/Configuration/` (Core, Api,
  Security, OpenApi), `app.ApplyMigrationsAsync()` in Infrastructure, `AddHealthChecks` + `MapHealthChecks`, a Hangfire section;
  `AddInfrastructure` split into `AddPersistence` / `AddIdentityAndJwt` / `AddBackgroundJobs`.
- **Changed:** the middleware order stays explicit in `Program.cs` (it is the part reviewers must read in one place);
  migrations still run in Development only; Hangfire recurring jobs stay inside `UseBackgroundJobs()` (honours
  `BackgroundJobs:Enabled`) instead of static `RecurringJob.AddOrUpdate` calls in `Program.cs`.
- **Rejected from the template:** `AllowAnyOrigin` CORS (the refresh cookie must not be usable from other sites),
  a dashboard open in every environment, `UseHttpsRedirection` (TLS terminates at the proxy in M6; HSTS already set outside
  Development), commented-out `UseAuthorization`; API versioning, messaging and observability extensions are added when M3/M5/M6
  need them.

### P-15 · Repository and Unit of Work
> "I found that you don't use repository and unit of work patterns, so please use them, and after each step update the
> Claude files and all related docs."

- **Accepted:** one specific repository per aggregate (`IAppointmentRepository`, `IDoctorRepository`, `IPatientRepository`,
  `IAuditRecordRepository`) plus `IUnitOfWork`. Contracts sit in Domain next to their aggregate, `internal sealed` EF Core
  implementations in `Infrastructure/Persistence/Repositories`. One commit per use case keeps the change and its audit row atomic.
  Application dropped the EF Core package, and new architecture tests keep it out of Application and Api (ADR-0016).
- **Changed:** removing EF from Application also removed the transitive `Microsoft.Extensions.Options` /
  `DependencyInjection.Abstractions`, so they are referenced explicitly now. Added the first service-level unit tests with fakes
  (validation before lookups, pre-check conflict commits nothing). That was not possible with `DbSet`s.
- **Rejected:** a generic `IRepository<T>` exposing `IQueryable` (leaks EF include/tracking semantics back into Application).
  Wrapping Identity's `UserManager` in another repository was also rejected: it already is one. The Identity adapters keep
  their explicit transactions.

### P-16 · Controllers hold endpoints only
> "Let the controller hold only the endpoints; private methods can go to the application layers with private helpers, to keep
> the controller clean."

- **Accepted:** `AuthController` loses its cookie constants and `Issue`/`CookieOptions` helpers, and every controller is
  endpoints-only. `ControllerConventionTests` (architecture tests) fail on any non-public method or constant in a controller.
- **Changed:** the refresh-cookie code went to `Api/Auth/RefreshTokenCookie`, not to Application. Writing a cookie needs
  `HttpResponse`, and Application may not reference ASP.NET Core (architecture rule). Use-case logic and its private helpers
  were already in the Application services.

### P-17 · Host config as C# 14 extension blocks, plus versioning, observability and messaging
> "Build extensions for WebApplicationBuilder like these [`public static class ApiConfig { extension(WebApplicationBuilder
> builder) { public WebApplicationBuilder AddApiConfiguration() … } }`, ApiVersioningConfig, MessagingConfig, ObservabilityConfig,
> SwaggerConfig]."

- **Accepted:**
  - `*Config` classes with `extension(WebApplicationBuilder)` / `extension(WebApplication)` blocks.
  - `ApiConfig` absorbs the old CoreConfiguration, like the template.
  - `AddProblemDetails()` is back as plumbing, while our own `IProblemDetailsWriter` keeps every body an `ApiResponse`.
  - API versioning, OpenTelemetry → Jaeger, and Wolverine + RabbitMQ (choices in the plan).
- **Changed (debugging):**
  - The .NET analyzers do not understand two extension blocks in one class: CA1708 reports them as members differing only
    by case. Disabled for `Api/Configuration/*.cs` only, with the reason written next to the setting.
  - A non-JSON body came back as an empty 415, because MVC sets `Content-Length: 0` and status-code pages skip any response
    that has a length. Fixed with an `IClientErrorFactory` that returns the envelope.
  - The status → code mapping is shared (`ErrorStatusMapping`) so the two paths can't drift.
  - Found and removed two stray lines of plugin hook output that had been committed into `.editorconfig` since M1.
- **Rejected:**
  - Swashbuckle: built-in OpenAPI + Scalar is kept (user choice).
  - FluentValidation MVC auto-validation with joined messages: Application validation is kept (user choice, ADR-0013).
- **Step 2 (versioning), debugging:** an unknown URL version (`/api/v2/doctors`) came back as 404 `ROUTE_NOT_FOUND`.
  Asp.Versioning deliberately treats a URL-segment version it doesn't serve as a missing path, and
  `UnsupportedApiVersionStatusCode` doesn't change that. The 404 stays, and the problem-details writer recognises
  `/api/v{n}/` with an unserved `n` and returns `UNSUPPORTED_API_VERSION` listing the supported versions (taken from the
  API explorer, not hard-coded). `[ApiVersion]` is not inherited from the base controller, so an architecture test makes sure
  every controller states it.
- **Step 3 (observability):** followed the template:
  - service name from config;
  - ASP.NET Core / HttpClient / EF Core instrumentation plus the `Wolverine` source;
  - OTLP exporter only when an endpoint is set; metrics exported only to a metrics backend.
  - **Changed:** dropped the correlation-id enrichment (we have no correlation header; W3C trace context covers it). Added an
    `enduser.id` tag (id only) and filtered out `/health` and `/hangfire`.
  - **Rejected:** Npgsql-level spans, because Hangfire's polling would flood them.
  - **Verified:** Jaeger listed the service with route-template request spans and EF Core database spans.
