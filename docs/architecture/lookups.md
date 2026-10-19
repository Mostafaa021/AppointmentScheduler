# Doctor and patient lookups 🟡

Read-only lists used to fill pickers: doctors, and a paged patient search. Patient create/update/deactivate is planned.
[Spec 005](../specs/005-patients.md) · requirements FR-005-x in
[functional.md](../requirements/functional.md#005--patients--spec--architecture).

## Components and flow (built)
```mermaid
sequenceDiagram
  participant UI as Book dialog (patient search box)
  participant C as DoctorsController / PatientsController
  participant Q as LookupQueries
  participant UoW as IUnitOfWork
  participant DB as PostgreSQL
  UI->>C: GET /api/v1/doctors
  C->>Q: ListDoctorsAsync
  Q->>UoW: Doctors.ListAsync — ordered by name
  UoW->>DB: SELECT … ORDER BY full_name
  C-->>UI: 200 [DoctorDto]
  UI->>C: GET /api/v1/patients?search=nora&page=1&pageSize=20
  C->>Q: SearchPatientsAsync — page ≥ 1, pageSize clamped to 1–100, term trimmed
  Q->>UoW: Patients.SearchAsync(term, page, pageSize)
  UoW->>DB: COUNT + page: lower(full_name) LIKE '%nora%' OR mrn = 'NORA'
  C-->>UI: 200 {items, page, pageSize, total}
```
Both are reads, so no `Idempotency-Key` is needed. If a future search moves to `POST /patients/search` (criteria in the
body), it gets `[SkipIdempotency]` on the action (plan 012).

## Patient lifecycle (planned, spec 005)
```mermaid
stateDiagram-v2
  [*] --> Active: POST /patients — MRN generated (MRN-000123)
  Active --> Active: PUT /patients/{id} — contact details
  Active --> Inactive: POST /patients/{id}/deactivate<br/>only without future Scheduled appointments (BR-6),<br/>else 422 PATIENT_HAS_FUTURE_APPOINTMENTS
  Inactive --> [*]
  note right of Inactive
    hidden from search unless includeInactive=true;
    booking → 422 PATIENT_INACTIVE
  end note
```
