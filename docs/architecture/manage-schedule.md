# Manage the day's schedule ✅

See a doctor's day, cancel, reschedule. [Spec 003](../specs/003-manage-schedule.md) · requirements FR-003-x in
[functional.md](../requirements/functional.md#003--manage-the-days-schedule--spec--architecture) ·
[ADR-0004 concurrency](../adr/0004-concurrency-etag.md).

## Components
| Layer | Piece | Job |
|---|---|---|
| Web | `features/schedule/schedule-page.ts`, `doctor-schedule-page.ts`, `cancel-dialog.ts`, `reschedule-dialog.ts` | day list; dialogs; a Doctor sees their own day read-only |
| Api | `AppointmentsController`: `GET /appointments?doctorId=&date=`, `GET /appointments/{id}`, `POST …/{id}/cancel`, `POST …/{id}/reschedule` | thin actions |
| Application | `AppointmentService.GetDayScheduleAsync / GetAsync / CancelAsync / RescheduleAsync`, `ScheduleAccess` | BR-7 read check, rules, one commit with audit |
| Domain | `Appointment.Cancel / Reschedule` | BR-3, BR-4, BR-5 |
| Infrastructure | `AppointmentRepository.ListDayScheduleAsync`, `GetByIdAsync` (tracked, from the generic base + doctor/patient), `OthersOverlapping` | `xmin` concurrency token on update |

## Appointment lifecycle
```mermaid
stateDiagram-v2
  [*] --> Scheduled: book (POST /appointments)
  Scheduled --> Scheduled: reschedule — new start, same doctor<br/>(future, valid slot, no overlap)
  Scheduled --> Cancelled: cancel (optional reason)
  Cancelled --> [*]
  note right of Scheduled
    Only while it hasn't started (BR-5).
    Otherwise 422 APPOINTMENT_NOT_MODIFIABLE
  end note
  note right of Cancelled
    Kept as history; frees the slot,
    because the constraints apply to Scheduled rows only
  end note
```

## Cancel
```mermaid
sequenceDiagram
  autonumber
  participant UI as Cancel dialog
  participant S as AppointmentService
  participant A as Appointment
  participant UoW as IUnitOfWork
  participant DB as PostgreSQL
  UI->>S: POST /appointments/{id}/cancel {reason?} + Idempotency-Key
  S->>S: validate (reason ≤ 500)
  S->>UoW: Appointments.GetByIdAsync(id) — tracked
  alt not found
    S-->>UI: 404 APPOINTMENT_NOT_FOUND
  end
  S->>A: Cancel(reason, now)
  alt already cancelled or started
    A-->>UI: 422 APPOINTMENT_NOT_MODIFIABLE
  end
  S->>UoW: AuditLog.Record(appointment.cancelled), SaveChangesAsync
  UoW->>DB: UPDATE … WHERE id = @id AND xmin = @version
  alt someone else changed it first
    DB-->>UI: 0 rows → 409 CONCURRENCY_CONFLICT
  end
  S-->>UI: 200 AppointmentDto (status Cancelled)
```

## Reschedule
```mermaid
sequenceDiagram
  autonumber
  participant UI as Reschedule dialog
  participant S as AppointmentService
  participant A as Appointment
  participant UoW as IUnitOfWork
  participant DB as PostgreSQL
  UI->>S: POST /appointments/{id}/reschedule {startUtc} + Idempotency-Key
  S->>UoW: Appointments.GetByIdAsync(id)
  S->>A: Reschedule(startUtc, DaySchedule, now) — BR-3/BR-4/BR-5 (422)
  S->>UoW: DoctorHasOverlapAsync / PatientHasOverlapAsync — ignoring itself
  alt conflict
    S-->>UI: 409 DOCTOR_CONFLICT / PATIENT_CONFLICT
  end
  S->>UoW: AuditLog.Record(appointment.rescheduled), SaveChangesAsync
  UoW->>DB: UPDATE (xmin check, exclusion constraints re-checked)
  S-->>UI: 200 AppointmentDto (new times)
```
The UI's reschedule dialog loads availability with `patientId`, so slots where the patient is busy elsewhere show as
PatientBusy ([availability](availability.md)).

## Who may do what (BR-7)
```mermaid
flowchart TD
  R[request] --> P{role policy}
  P -- Receptionist / Admin --> OK[read + change any doctor's schedule]
  P -- Doctor only, change --> F1[403 FORBIDDEN]
  P -- Doctor only, read --> C{ScheduleAccess:<br/>doctorId == own doctor?}
  C -- yes --> OK2[read own schedule]
  C -- no / no doctor link --> F2[403 FORBIDDEN — fails closed]
```
Details: [auth-and-users.md](auth-and-users.md).
