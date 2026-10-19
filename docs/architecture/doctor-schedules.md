# Doctor schedules 📋 planned

Per-doctor working hours, breaks, time off and appointment types. Today every doctor works the clinic hours from
`Clinic:Open` / `Close` / `SlotMinutes`. [Spec 006](../specs/006-doctor-schedules.md) · requirements FR-006-x in
[functional.md](../requirements/functional.md#006--doctors-working-hours-time-off-appointment-types--spec--architecture).

## Data (from the spec)
```mermaid
erDiagram
  doctors ||--o{ working_hours : "weekly template"
  doctors ||--o{ time_off : "absences"
  appointment_types ||--o{ appointments : "duration"
  doctors ||--o{ appointments : "has"
  working_hours {
    int doctor_id FK
    int day_of_week
    time start_local
    time end_local
  }
  time_off {
    int doctor_id FK
    timestamptz start_utc
    timestamptz end_utc
    string reason
  }
  appointment_types {
    int id PK
    string name "Consultation 30, Extended 60"
    int duration_minutes "multiple of the slot size"
  }
  appointments {
    uuid id PK
    int appointment_type_id FK
  }
```

## How availability would change
```mermaid
flowchart TD
  D[doctor + date] --> WH[that weekday's working-hour ranges<br/>gaps between ranges = breaks]
  WH --> TO[remove time off overlapping the day]
  TO --> HOL[remove clinic holidays — spec 007]
  HOL --> FIT[offer only starts where the appointment type's<br/>whole duration fits]
  FIT --> CLS[classify Free / Booked / Past / PatientBusy<br/>as today — availability.md]
  WH -. outside hours .-> UN[Unavailable]
```
Changing hours never deletes appointments: those now outside hours are flagged `outsideWorkingHours: true` in the day
schedule. The default seed (Mon–Fri 09:00–17:00, 30 minutes) keeps today's behaviour.
