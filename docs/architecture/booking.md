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
  S->>UoW: Appointments.DoctorHasOverlapAsync / PatientHasOverlapAsync
  alt overlap found
    S-->>C: 409 DOCTOR_CONFLICT / PATIENT_CONFLICT (nothing staged)
  end
  S->>UoW: Appointments.Add + AuditLog.Record(appointment.booked)
  S->>UoW: SaveChangesAsync — appointment + audit row, one transaction
  UoW->>DB: INSERT appointments, INSERT audit_records
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
  API->>DB: pre-check for A — no overlap
  API->>DB: pre-check for B — no overlap (A not committed yet)
  API->>DB: INSERT A
  DB-->>API: ok
  API-->>R1: 201 Created
  API->>DB: INSERT B
  DB-->>API: 23P01 exclusion violation (ex_appointments_doctor_overlap)
  API-->>R2: 409 DOCTOR_CONFLICT
```
The pre-check gives a precise error in the common case. Only the constraint is safe under concurrency, and its violation
is translated to the same code, so clients see one behaviour.

## Where each rule is enforced
```mermaid
flowchart LR
  UI[UI: only Free slots<br/>are clickable] -->|UX only, may be stale| API
  subgraph API[API]
    V[Validator: reason 1–500] --> E[Exists: doctor, patient] --> D[Appointment: BR-3 past,<br/>BR-4 grid + hours] --> P[Pre-check: BR-1, BR-2<br/>precise 409]
  end
  P --> DB[(Exclusion constraints:<br/>BR-1, BR-2 guarantee)]
```
