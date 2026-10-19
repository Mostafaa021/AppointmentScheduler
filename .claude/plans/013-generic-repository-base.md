# 013 — Generic repository base for every repository

- Status: **Done** · Approved 2026-10-20 · ADR-0016 (amendment)
- Progress: contracts ☑ · base + implementations ☑ · arch tests ☑ · docs ☑ · live check ☑ (commits only when the user says so)

## Context
Each repository was written from scratch, so common members (`ExistsAsync`, `Add`, get-by-id) were repeated across
`IAppointmentRepository`, `IDoctorRepository`, `IPatientRepository`, `IAuditRecordRepository` and their implementations. The
user wants one generic repository with the common functionality, inherited by every repository.

The user chose these members: **Add, GetByIdAsync (tracked), GetByIdReadOnlyAsync, ExistsAsync**.
- There is no Remove, so the audit trail stays append-only.
- There is no list-everything method, because appointments and audit rows have no upper bound.
- Specific queries stay named methods.
- Nothing returns `IQueryable`: the generic *IQueryable* repository stays rejected (ADR-0016).

## Changes
1. **Domain/Common**:
   - `IEntity<TKey>` (`TKey Id { get; }`), implemented by `Appointment`, `AuditRecord`, `Doctor` and `Patient`;
   - `IRepository<TEntity, TKey>` with `Add`, `GetByIdAsync`, `GetByIdReadOnlyAsync` and `ExistsAsync`.
2. **Specific contracts** extend it and drop the members they now inherit.
3. **`Infrastructure/Persistence/Repositories/Repository.cs`**: an `internal abstract` base with `Db`, `Set` and a
   `WithDetails` hook for what a get-by-id loads. It implements the four members with `e.Id.Equals(id)`.
4. **Implementations** inherit it:
   - `AppointmentRepository` overrides `WithDetails` (doctor + patient);
   - the others lose their duplicated members.
5. **Architecture tests**: contracts extend `IRepository<,>`; implementations inherit `Repository<,>`.
6. **Unit-test fakes** implement the inherited members.
7. **Docs:**
   - ADR-0016, CLAUDE.md;
   - the `/new-feature` skill, the backend-developer and code-reviewer agents;
   - CHANGELOG, PROMPTS.

## Verification
- Build, then unit and architecture tests.
- **Live** (no integration tests; proves EF translates the generic `Equals`):
  - availability;
  - book with doctor and patient names in the response;
  - cancel;
  - an unknown doctor → 404 `DOCTOR_NOT_FOUND`.
