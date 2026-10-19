# ADR-0016 — Repository per aggregate + Unit of Work; Application has no EF Core

- Status: Accepted · Date: 2026-10-19 · Supersedes ADR-0001's "Application talks to EF through `IAppDbContext`"

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
- **Implementations**: `Infrastructure/Persistence/Repositories/*`, all `internal sealed`, sharing the request-scoped
  `AppDbContext`. `UnitOfWork` delegates to `AppDbContext.SaveChangesAsync`, which still translates 23P01 and stale `xmin`
  into `ConflictException` (ADR-0002, ADR-0004). Registered in `AddPersistence`.
- **Identity stays an adapter.** `IdentityAuthService` / `IdentityUserAdminService` live in Infrastructure and keep using
  `UserManager` and `AppDbContext`, including explicit transactions for refresh rotation. `UserManager` is itself the
  repository for users. They still write audit rows through `AuditLog` → `IAuditRecordRepository`, committed by their
  `SaveChangesAsync`.
- **Enforced** by `ArchitectureTests`: Application has no dependency on `Microsoft.EntityFrameworkCore`; Api has none either;
  repository and unit-of-work implementations are not public.

## Alternatives
- Keep `IAppDbContext`: least code, composable LINQ, but EF in Application and no unit tests without a DB.
- Generic `IRepository<T>` with `IQueryable`: leaks EF semantics such as `Include` and tracking back into Application.
- CQRS read models: overkill at this size.

## Consequences
+ Application is persistence-ignorant (no EF package). Use-case order can be unit-tested with fakes (`AppointmentServiceTests`).
  Every query shape lives in one place.
− More files and interfaces. Each new query needs a repository method instead of inline LINQ. Projections now load entities
  (fine at clinic scale; add a projection method if a list grows large).
