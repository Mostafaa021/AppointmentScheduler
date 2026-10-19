---
name: new-feature
description: Single-session, spec-first checklist for adding a feature end to end (spec → tests → domain → application → infrastructure → api → web → docs) without dispatching role agents. Use for medium changes when the full /sdlc ceremony is not wanted.
---
# /new-feature — spec-first in one session

0. **Plan** — once the approach is approved, save it as `.claude/plans/NNN-<slug>.md` and add it to `.claude/plans/README.md`
   (CLAUDE.md, Workflow 0). Amend that file if the plan changes; tick its Progress line per step.
1. **Spec** — copy `docs/specs/_template.md` to `docs/specs/NNN-<name>.md`; write AC `AC-NNN-x` (Given/When/Then, incl. status
   and `errorCode`). Fill the API contract table with **every** status per route. Show the AC list to the user before coding.
2. **Tests first** — one failing test per AC, named `AC_NNN_x_<Behavior>`:
   rules and use cases → `tests/AppointmentScheduler.UnitTests/<Feature>/` (fake `IUnitOfWork`, `FakeTimeProvider`); layer
   rules → `tests/AppointmentScheduler.ArchitectureTests`. Assert the exception type and `ErrorCodes.X`. No integration tests,
   Testcontainers or `WebApplicationFactory` (plan 011): check HTTP/DB behaviour live with `curl` and note it in the spec.
3. **Domain** — `src/AppointmentScheduler.Domain/<Feature>/`: entity methods/policies enforce rules and throw a
   `SchedulerException` subclass (`BusinessRuleException` 422, `ConflictException` 409, `NotFoundException` 404, …).
   New business codes go in `Domain/Common/ErrorCodes.cs` under a `// ── <Feature> ──` section, UPPER_SNAKE — never a string
   literal. Codes only the HTTP pipeline produces go in `Api/Errors/ApiErrorCodes.cs`.
   Diagnostics for operators go in `LogDetail` (ids only):
   ```csharp
   throw new ConflictException(ErrorCodes.ClinicClosed, "The clinic is closed on this day.") { LogDetail = $"clinic {clinicId} {date}" };
   ```
   New aggregate → entity implements `IEntity<TKey>`, contract `I<Aggregate>Repository : IRepository<<Aggregate>, TKey>`
   (inherits Add / GetById / GetByIdReadOnly / Exists, plan 013). New query → a named method on that contract
   (`Domain/<Feature>/I<Aggregate>Repository.cs`, ADR-0016). It returns entities, never `IQueryable`; tracked only when the
   caller will change the entity.
4. **Application** — `src/AppointmentScheduler.Application/<Feature>/`: service method + request/response records +
   (DTOs expose Application types only — an Application enum, never a Domain one: the Api must not reference Domain) +
   FluentValidation validator (`ValidateOrThrow`); register in `Application/DependencyInjection.cs`.
   Inject `IUnitOfWork` only and reach repositories through it (`unitOfWork.Appointments…`), never inject a repository;
   stage changes, then **one** `await unitOfWork.SaveChangesAsync(ct)` per use case (after `audit.Record(...)`).
   No EF Core here (architecture test). Use-case order can be unit-tested with fakes (see `AppointmentServiceTests`).
5. **Infrastructure** — EF configuration in `Infrastructure/Persistence/Configurations/`; schema change → `/add-migration`.
   Repository implementation `internal sealed` in `Infrastructure/Persistence/Repositories/`, inheriting
   `Repository<TEntity, TKey>` (override `WithDetails` for navigations); a new repository also gets a
   property on `IUnitOfWork` / `UnitOfWork` (not a DI registration). Other services go in `AddPersistence`
   (`Infrastructure/DependencyInjection.cs`), never in `Program.cs`. A new host-level
   concern (e.g. rate limiting) gets `Api/Configuration/<Concern>Config.cs`, chained in `Program.cs`:
   ```csharp
   public static class RateLimitingConfig
   {
       extension(WebApplicationBuilder builder)
       {
           public WebApplicationBuilder AddRateLimitingConfiguration()
           {
               builder.Services.AddRateLimiter(o => { /* policies */ });
               return builder;
           }
       }

       extension(WebApplication app)
       {
           public WebApplication UseRateLimitingConfiguration() { app.UseRateLimiter(); return app; }
       }
   }
   ```
6. **Api** — action on `Api/Controllers/<Feature>Controller.cs` (inherits `ApiControllerBase`); thin; correct policy,
   The controller class carries `[ApiVersion(1.0)]` + `[Route("api/v{version:apiVersion}/<feature>")]` (ADR-0017).
   POSTs need nothing extra for idempotency: the global filter requires the `Idempotency-Key` and stores/replays responses;
   just list `400`/`409`/`422` in `[ProducesResponseType]` (key missing / in progress / key reused). PUT/PATCH/DELETE need
   no key, so make them idempotent by design. A POST that only reads (search criteria in the body) gets `[SkipIdempotency]`
   on the action (plan 012).
   rate-limit and idempotent group. Return `ApiResponse<T>`, declare every status the action can produce (401/500 come from
   the base), no `ModelState.IsValid` check (`[ApiController]` already returns 400 `BAD_REQUEST`):
   ```csharp
   [HttpPost]
   [Authorize(Policy = Policies.FrontDesk)]
   [EndpointSummary("Book a free slot. 409 DOCTOR_CONFLICT / PATIENT_CONFLICT, 422 APPOINTMENT_IN_PAST / INVALID_SLOT.")]
   [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status201Created)]
   [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
   [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
   [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
   [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
   [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
   public async Task<IActionResult> Book(
       [FromBody] BookAppointmentRequest request,
       CancellationToken cancellationToken = default)
   {
       var created = await appointments.BookAsync(request, cancellationToken);
       return CreatedAtRoute("GetAppointment", new { id = created.Id },
           ApiResponse<AppointmentDto>.Created(created, "Appointment booked."));
   }
   ```
   Reads: `return OkResponse(dto);` — with a short `message` for mutations (`OkResponse(dto, "Appointment cancelled.")`).
   Controllers hold endpoints only: no private helpers or constants. Put the logic in the Application service, and
   HTTP-only plumbing (cookies, headers) in an Api helper class (e.g. `Api/Auth/RefreshTokenCookie`). Enforced by
   `ControllerConventionTests`.
   Controllers hold endpoints only: no private helpers or constants. Put the logic in the Application service, and
   HTTP-only plumbing (cookies, headers) in an Api helper class (e.g. `Api/Auth/RefreshTokenCookie`). Enforced by
   `ControllerConventionTests`.
7. **Web** — `web/src/app/core/api/` service + model typed with the `data` DTO (the `apiResponseInterceptor` unwraps it),
   `web/src/app/features/<feature>/` component + spec; branch on `error.errorCode`, flush envelope bodies in specs.
8. **Verify** — `/verify`.
9. **Docs** — tick the spec Status, update `SPEC.md` §3 (routes and error-code table if API/data changed), `CHANGELOG.md`,
   `/log-prompt`.
