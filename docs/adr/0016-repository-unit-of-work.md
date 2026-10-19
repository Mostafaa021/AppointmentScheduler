# ADR-0016 — Repository per aggregate + Unit of Work; Application has no EF Core

- Status: Accepted · Date: 2026-10-19 · Supersedes ADR-0001's "Application talks to EF through `IAppDbContext`"
- Amended 2026-10-20 (user, plan 010): **the unit of work is the only way to a repository.** `IUnitOfWork` exposes
  `Appointments`, `Doctors`, `Patients` and `AuditRecords`; services inject `IUnitOfWork` alone; repositories are not
  registered in DI.

## Context
Application services queried `IAppDbContext` `DbSet`s directly, so Application referenced EF Core. Query shapes
(includes, tracking, the half-open overlap predicate) were repeated across services. Services could not be unit-tested without
a database. The team's house style uses repositories and a unit of work.

## Decision
- **Contracts in Domain, next to the aggregate they persist**. Domain still references nothing, because the contracts use only
  domain types, `Task` and `CancellationToken`:
  - `Domain/Appointments/IAppointmentRepository`
  - `Domain/Doctors/IDoctorRepository`
  - `Domain/Patients/IPatientRepository`
  - `Domain/Common/IAuditRecordRepository`
  - `Domain/Common/IUnitOfWork`
- **Specific repositories, no generic `IRepository<T>`**. Each method is a named query the use cases need:
  - `GetByIdAsync` is tracked, for changes; `GetByIdReadOnlyAsync` is not.
  - `ListDayScheduleAsync`, `ListScheduledForDoctorAsync`, `ListScheduledForPatientAsync`.
  - `DoctorHasOverlapAsync` / `PatientHasOverlapAsync` for the BR-1/BR-2 pre-checks.
  - `ExistsAsync`; `SearchAsync` returns `(Items, Total)` for paging.

  They return domain entities, and Application maps them to DTOs. No `IQueryable` leaves Infrastructure.
- **Repositories stage, the unit of work commits.** `Add` / tracked changes are staged, and `IUnitOfWork.SaveChangesAsync`
  commits once per use case. The appointment and its audit row therefore succeed or fail together (ADR-0011).
- **The unit of work owns the repositories.** Services inject `IUnitOfWork` only and call `unitOfWork.Appointments.Add(...)`,
  `unitOfWork.Doctors.ExistsAsync(...)`, then `unitOfWork.SaveChangesAsync(ct)`. Repositories and commit visibly share one
  context, and no service can obtain a repository any other way.
- **Implementations**: `Infrastructure/Persistence/Repositories/*`, all `internal sealed`. `UnitOfWork` creates each
  repository on first use over the request-scoped `AppDbContext` (only `IUnitOfWork` is registered in `AddPersistence`) and
  delegates to `AppDbContext.SaveChangesAsync`, which still translates 23P01 and stale `xmin` into `ConflictException`
  (ADR-0002, ADR-0004). A new repository = contract + implementation + a property on `IUnitOfWork` / `UnitOfWork`.
- **Identity stays an adapter.** `IdentityAuthService` / `IdentityUserAdminService` live in Infrastructure and keep using
  `UserManager` and `AppDbContext`, including explicit transactions for refresh rotation. `UserManager` is itself the
  repository for users. They still write audit rows through `AuditLog` → `IUnitOfWork.AuditRecords`, committed by their
  `SaveChangesAsync` on the same context.
- **Enforced** by `ArchitectureTests`: Application has no dependency on `Microsoft.EntityFrameworkCore`; Api has none either;
  repository and unit-of-work implementations are not public; no constructor in Application or Infrastructure takes a
  repository (`Repositories_are_reached_only_through_the_unit_of_work`).

## Alternatives
- Keep `IAppDbContext`: least code, composable LINQ, but EF in Application and no unit tests without a DB.
- Generic `IRepository<T>` with `IQueryable`: leaks EF semantics such as `Include` and tracking back into Application.
- CQRS read models: overkill at this size.

## Consequences
+ Application is persistence-ignorant (no EF package). Use-case order can be unit-tested with fakes (`AppointmentServiceTests`).
  Every query shape lives in one place.
+ One injected dependency for all data access; unit tests hand the service one fake unit of work holding fake repositories.
− More files and interfaces. Each new query needs a repository method instead of inline LINQ. Projections now load entities
  (fine at clinic scale; add a projection method if a list grows large).
