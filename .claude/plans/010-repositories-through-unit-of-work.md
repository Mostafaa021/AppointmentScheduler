# 010 — Repositories only through the unit of work

- Status: **Done** · Approved 2026-10-20 · ADR-0016 (amendment)
- Progress: arch test ☑ · IUnitOfWork ☑ · services ☑ · docs ☑ (ticked as verified; commits only when the user says so)

## Context
The Application services got their repositories straight from DI, next to `IUnitOfWork`, which only had `SaveChangesAsync`:
- `AppointmentService(IAppointmentRepository, IDoctorRepository, IPatientRepository, IUnitOfWork, …)`;
- `AvailabilityService(IDoctorRepository, IAppointmentRepository, …)`;
- `LookupQueries(IDoctorRepository, IPatientRepository)`;
- `AuditLog(IAuditRecordRepository, …)`.

The user wants the unit of work to be the single entry point: a service injects `IUnitOfWork` only, and reaches every
repository through it, e.g. `unitOfWork.Appointments.Add(...)`, then `unitOfWork.SaveChangesAsync()`. The repositories and the
commit then visibly share one `AppDbContext`, and no service can pull a repository from somewhere else.

## Changes
1. **`Domain/Common/IUnitOfWork.cs`**: the repositories are properties (`Appointments`, `Doctors`, `Patients`,
   `AuditRecords`), next to `SaveChangesAsync`.
2. **`Infrastructure/Persistence/Repositories/UnitOfWork.cs`**:
   - creates each repository on first use over its `AppDbContext`, with the C# 14 `field` keyword
     (`public IAppointmentRepository Appointments => field ??= new AppointmentRepository(db);`);
   - the repositories keep their `AppDbContext` constructors.
3. **`Infrastructure/DependencyInjection.cs`**: only `AddScoped<IUnitOfWork, UnitOfWork>()`. The four repository
   registrations are removed, so a repository can't be injected at all.
4. **Services take `IUnitOfWork` instead of repositories:**
   - `AppointmentService`, `AvailabilityService`, `LookupQueries`;
   - `AuditLog` → `unitOfWork.AuditRecords.Add(...)`.
   - The Identity adapters keep using `AuditLog` plus their own `db.SaveChangesAsync` / transactions on the same scoped
     `AppDbContext`, so nothing changes for them.
5. **Architecture test** `Repositories_are_reached_only_through_the_unit_of_work`:
   - reflection over the Application and Infrastructure assemblies; no constructor parameter's type name may end with
     `Repository`;
   - written first: it failed, naming exactly the four classes above, then passed after step 4.
6. **Unit tests**: `AppointmentServiceTests.FakeUnitOfWork` hands out the fake repositories; the service gets only the unit
   of work.
7. **Docs:**
   - ADR-0016 amended;
   - CLAUDE.md data-access bullets;
   - architecture.md;
   - backend-developer, code-reviewer and architect agents, and the `/new-feature` skill;
   - CHANGELOG ("Changed"), PROMPTS.

## Verification
- `dotnet build -warnaserror` passes.
- `dotnet test`: 60 unit + 14 architecture + 123 integration tests, all green.
  - The integration tests prove DI still resolves every service and that the change and its audit row still commit together.
  - One full run hit the known flake in `ObservabilityTests.Request_span_is_tagged_with_the_user_id_only`: a process-wide
    `ActivityListener` with `Assert.Single`. It passed alone and on the re-run.
- No constructor in `src/` takes a repository (grep).
- No live check: there is no behaviour change.
