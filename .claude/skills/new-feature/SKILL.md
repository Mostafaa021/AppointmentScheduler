---
name: new-feature
description: Single-session, spec-first checklist for adding a feature end to end (spec → tests → domain → application → infrastructure → api → web → docs) without dispatching role agents. Use for medium changes when the full /sdlc ceremony is not wanted.
---
# /new-feature — spec-first in one session

1. **Spec** — copy `docs/specs/_template.md` to `docs/specs/NNN-<name>.md`; write AC `AC-NNN-x` (Given/When/Then, incl. status
   and `errorCode`). Fill the API contract table with **every** status per route. Show the AC list to the user before coding.
2. **Tests first** — one failing test per AC, named `AC_NNN_x_<Behavior>`:
   rules → `tests/AppointmentScheduler.UnitTests/<Feature>/`; HTTP/DB → `tests/AppointmentScheduler.IntegrationTests/<Feature>/`.
   Read success bodies with `DataAsync()`, errors with `ErrorCodeAsync()`, and assert against `ErrorCodes.X`.
3. **Domain** — `src/AppointmentScheduler.Domain/<Feature>/`: entity methods/policies enforce rules and throw a
   `SchedulerException` subclass (`BusinessRuleException` 422, `ConflictException` 409, `NotFoundException` 404, …).
   New codes go in `Domain/Common/ErrorCodes.cs` under a `// ── <Feature> ──` section, UPPER_SNAKE — never a string literal.
   Diagnostics for operators go in `LogDetail` (ids only):
   ```csharp
   throw new ConflictException(ErrorCodes.ClinicClosed, "The clinic is closed on this day.") { LogDetail = $"clinic {clinicId} {date}" };
   ```
   New aggregate or new query → add a named method to the aggregate's repository contract
   (`Domain/<Feature>/I<Aggregate>Repository.cs`, ADR-0016). It returns entities, never `IQueryable`; tracked only when the
   caller will change the entity.
4. **Application** — `src/AppointmentScheduler.Application/<Feature>/`: service method + request/response records +
   FluentValidation validator (`ValidateOrThrow`); register in `Application/DependencyInjection.cs`. Data access only via
   repositories; stage changes, then **one** `await unitOfWork.SaveChangesAsync(ct)` per use case (after `audit.Record(...)`).
   No EF Core here (architecture test). Use-case order can be unit-tested with fakes (see `AppointmentServiceTests`).
5. **Infrastructure** — EF configuration in `Infrastructure/Persistence/Configurations/`; schema change → `/add-migration`.
   Repository implementation `internal sealed` in `Infrastructure/Persistence/Repositories/`, registered in `AddPersistence`
   (`Infrastructure/DependencyInjection.cs`). Never in `Program.cs`. A new host-level
   concern (e.g. rate limiting, OpenTelemetry) gets `Api/Configuration/<Concern>Configuration.cs` with an
   `Add<Concern>Configuration()` extension, chained in `Program.cs`.
6. **Api** — action on `Api/Controllers/<Feature>Controller.cs` (inherits `ApiControllerBase`); thin; correct policy,
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
7. **Web** — `web/src/app/core/api/` service + model typed with the `data` DTO (the `apiResponseInterceptor` unwraps it),
   `web/src/app/features/<feature>/` component + spec; branch on `error.errorCode`, flush envelope bodies in specs.
8. **Verify** — `/verify`.
9. **Docs** — tick the spec Status, update `SPEC.md` §3 (routes and error-code table if API/data changed), `CHANGELOG.md`,
   `/log-prompt`.
