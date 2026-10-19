# Booking ✅

Book a free slot for a patient, never double-booking. [Spec 002](../specs/002-booking.md) · requirements FR-002-x in
[functional.md](../requirements/functional.md#002--booking--spec--architecture) ·
[ADR-0002 exclusion constraints](../adr/0002-postgres-exclusion-constraints.md).

## Components
| Layer | Piece | Job |
|---|---|---|
| Web | `features/schedule/book-dialog.ts`; `core/http/idempotency-key.interceptor.ts` | patient search → reason → review → confirm; one `Idempotency-Key` per confirm |
| Api | `Controllers/AppointmentsController.cs` `POST /api/v1/appointments`; `Idempotency/IdempotencyFilter.cs` | replay or run; 201 + `Location` |
| Application | `Appointments/AppointmentService.BookAsync`, `AppointmentValidators.cs`, `ScheduleAccess` | validate → exists → rules → pre-check → stage → one commit |
| Domain | `Appointments/Appointment.Schedule` (BR-3, BR-4 via `DaySchedule`) | the aggregate refuses past or invalid slots |
| Infrastructure | `AppointmentRepository` (`DoctorHasOverlapAsync`, `PatientHasOverlapAsync`, `Add`), `UnitOfWork`, `AppDbContext.SaveChangesAsync` | pre-check queries; 23P01 → 409 |
| Database | `ex_appointments_doctor_overlap`, `ex_appointments_patient_overlap` | the guarantee (BR-1, BR-2) |

## Happy path
```mermaid
sequenceDiagram
  autonumber
  participant UI as Angular book dialog
  participant F as IdempotencyFilter
  participant C as AppointmentsController
  participant S as AppointmentService
  participant A as Appointment (Domain)
  participant UoW as IUnitOfWork
  participant DB as PostgreSQL
  UI->>F: POST /appointments {doctorId, patientId, startUtc, reason}<br/>Idempotency-Key: K
  F->>DB: claim (user, K) InProgress
  F->>C: run
  C->>S: BookAsync(request)
  S->>S: validate (400 VALIDATION_FAILED)
  S->>UoW: Doctors.ExistsAsync, Patients.ExistsAsync (404)
  S->>A: Schedule(…, DaySchedule, now) — BR-3 past (422), BR-4 grid/hours (422)
  S->>UoW: ExecuteInTransactionAsync — BEGIN
  S->>UoW: Appointments.LockScheduleAsync(doctor, patient)
  UoW->>DB: pg_advisory_xact_lock(doctor), then (patient) — a competing booking waits here
  S->>UoW: Appointments.DoctorHasOverlapAsync / PatientHasOverlapAsync
  alt overlap found
    S-->>C: 409 DOCTOR_CONFLICT / PATIENT_CONFLICT (ROLLBACK, nothing staged)
  end
  S->>UoW: Appointments.Add + AuditLog.Record(appointment.booked)
  S->>UoW: SaveChangesAsync, then COMMIT — appointment + audit row together, locks released
  UoW->>DB: INSERT appointments, INSERT audit_records, COMMIT
  S->>UoW: Appointments.GetByIdReadOnlyAsync (with doctor + patient)
  S-->>C: AppointmentDto
  C-->>F: 201 + Location
  F->>DB: store response, Completed
  F-->>UI: 201 ApiResponse
```

## Two receptionists, one slot (NFR-1)
```mermaid
sequenceDiagram
  participant R1 as Receptionist A
  participant R2 as Receptionist B
  participant API
  participant DB as PostgreSQL
  R1->>API: POST /appointments (Dr 1, 09:30)
  R2->>API: POST /appointments (Dr 1, 09:30)
  API->>DB: A: BEGIN, lock doctor 1 (+ patient)
  API->>DB: B: BEGIN, lock doctor 1 — waits
  API->>DB: A: pre-check (no overlap), INSERT, COMMIT
  API-->>R1: 201 Created
  DB-->>API: B gets the lock
  API->>DB: B: pre-check sees A, ROLLBACK
  API-->>R2: 409 DOCTOR_CONFLICT
```
Without the lock, both pre-checks pass and both INSERTs wait on each other inside the exclusion constraint. PostgreSQL
then aborts victims as deadlocks (40P01), one per second: measured 7 of 8 losers as 503 after up to 7 s. With the lock,
the losers queue for milliseconds and get a precise 409 (SPEC §5). The constraint still guarantees correctness for any
write that skips the lock, and if such a write deadlocks, `AppDbContext` maps the 40P01 to 409 `CONCURRENCY_CONFLICT`.

```mermaid
flowchart TD
  W[booking or reschedule] --> T[BEGIN]
  T --> L1[lock doctor key 1:doctorId]
  L1 --> L2[lock patient key 2:patientId<br/>always this order → locks never deadlock]
  L2 --> P{pre-check overlap?}
  P -- yes --> R[ROLLBACK → 409 DOCTOR_ / PATIENT_CONFLICT]
  P -- no --> I[INSERT / UPDATE + audit row]
  I --> X{exclusion constraint}
  X -- ok --> C[COMMIT → 201 / 200, locks released]
  X -- 23P01 --> R
```

## Where each rule is enforced
```mermaid
flowchart LR
  UI[UI: only Free slots<br/>are clickable] -->|UX only, may be stale| API
  subgraph API[API]
    V[Validator: reason 1–500] --> E[Exists: doctor, patient] --> D[Appointment: BR-3 past,<br/>BR-4 grid + hours] --> P[Pre-check: BR-1, BR-2<br/>precise 409]
  end
  P --> DB[(Exclusion constraints:<br/>BR-1, BR-2 guarantee)]
```
