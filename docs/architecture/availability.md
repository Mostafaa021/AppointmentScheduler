# Availability ✅

Show a doctor's day as bookable slots in the clinic's time zone. [Spec 001](../specs/001-availability.md) ·
requirements FR-001-x in [functional.md](../requirements/functional.md#001--availability--spec--architecture) ·
[ADR-0003 time zones](../adr/0003-time-zones.md).

## Components
| Layer | Piece | Job |
|---|---|---|
| Web | `features/schedule/schedule-page.ts`, `slot-grid.ts` | doctor + date pickers; colours slots; only Free is clickable |
| Api | `Controllers/DoctorsController.cs` → `GET /api/v1/doctors/{id}/availability?date=&patientId=` | bind, call the service, wrap in `ApiResponse` |
| Application | `Availability/AvailabilityService.cs` | doctor exists → slots → two queries → classify |
| Domain | `Scheduling/DaySchedule.cs` | clinic-local slots for a date, converted to UTC (DST-safe); `UtcWindowOf(date)` |
| Infrastructure | `AppointmentRepository.ListScheduledForDoctorAsync` / `…ForPatientAsync`, `DoctorRepository` (`ExistsAsync` from the generic base) | half-open overlap queries, `AsNoTracking` |

## Request flow
```mermaid
sequenceDiagram
  autonumber
  participant UI as Angular schedule page
  participant API as DoctorsController
  participant S as AvailabilityService
  participant DS as DaySchedule (Domain)
  participant UoW as IUnitOfWork
  participant DB as PostgreSQL
  UI->>API: GET /api/v1/doctors/1/availability?date=2026-10-28&patientId=10
  API->>S: GetAsync(doctorId, date, patientId)
  S->>UoW: Doctors.ExistsAsync(1)
  UoW->>DB: SELECT EXISTS … doctors
  alt unknown doctor
    S-->>API: NotFoundException DOCTOR_NOT_FOUND
    API-->>UI: 404 ApiResponse
  end
  S->>DS: SlotsFor(date) + UtcWindowOf(date)
  DS-->>S: 16 slots 09:00–16:30 local, as UTC
  S->>UoW: Appointments.ListScheduledForDoctorAsync(1, from, to)
  S->>UoW: Appointments.ListScheduledForPatientAsync(10, from, to)
  UoW->>DB: 2 queries (status = Scheduled, start < to AND end > from)
  S->>S: classify each slot
  S-->>API: AvailabilityDto {doctorId, date, timeZone, slots}
  API-->>UI: 200 ApiResponse
```
Always two appointment queries, however many slots the day has (NFR-2).

## Slot classification
```mermaid
flowchart TD
  A[slot start..end, UTC] --> B{a Scheduled appointment<br/>of this doctor overlaps?}
  B -- yes --> BOOKED[Booked<br/>+ appointmentId]
  B -- no --> C{slot start ≤ now?<br/>TimeProvider}
  C -- yes --> PAST[Past]
  C -- no --> D{patientId given and that patient<br/>has an overlapping Scheduled<br/>appointment with any doctor?}
  D -- yes --> BUSY[PatientBusy]
  D -- no --> FREE[Free — bookable]
```
Overlap is half-open, like the exclusion constraints: `a.start < slot.end && a.end > slot.start`, so 09:00–09:30 and
09:30–10:00 never overlap. A cancelled appointment is not *Scheduled*, so its slot shows Free again. `Unavailable`
(holidays, time off, outside working hours) arrives with specs 006/007.

## Time zones
```mermaid
flowchart LR
  D[date 2026-03-29<br/>clinic-local] --> L[09:00, 09:30 … 16:30<br/>local wall clock]
  L -->|TimeZoneInfo<br/>Europe/London| U[UTC instants<br/>offset changes on DST days]
  U --> R[response: startUtc, endUtc,<br/>localStart 'HH:mm', timeZone]
  R --> UI[UI shows localStart,<br/>posts back startUtc]
```
The browser's own time zone never matters ([ADR-0003](../adr/0003-time-zones.md)).
