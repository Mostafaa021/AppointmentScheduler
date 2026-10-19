# 006 — Doctors, working hours, breaks, time off, appointment types

| Status | Refined ☑ · Designed ☐ · Built ☐ · Tested ☐ · Reviewed ☐ · Released ☐ · Documented ☐ |
|---|---|
| Milestone | M4 |
| Business rules touched | BR-4 |

## Goal
Admins describe when each doctor works so availability is accurate, and visits can have different lengths.

## Acceptance criteria
- **AC-006-1** Admin CRUD for doctors (`fullName`, `specialty`, `clinicId`, `slotMinutes` ∈ {15, 30, 60}, `isActive`).
- **AC-006-2** `PUT /doctors/{id}/working-hours` replaces the weekly template: list of `{dayOfWeek, startLocal, endLocal}`;
  ranges on the same day must not overlap and must align to `slotMinutes`; a gap between two ranges is a break.
- **AC-006-3** `POST /doctors/{id}/time-off {startUtc, endUtc, reason}`; overlapping existing Scheduled appointments are returned
  as a warning list (not auto-cancelled).
- **AC-006-4** Appointment types (`Consultation 30`, `Extended 60`, …) are reference data; duration must be a multiple of the
  doctor's slot size.
- **AC-006-5** Default seed: every doctor works Mon–Fri 09:00–17:00 with 30-min slots, so M1 behavior is unchanged.
- **AC-006-6** Changing working hours never deletes existing appointments; appointments now outside hours are flagged in the
  day schedule (`outsideWorkingHours: true`).

## Data changes
`working_hours(doctor_id, day_of_week, start_local time, end_local time)`, `time_off(doctor_id, start_utc, end_utc, reason)`,
`appointment_types(id, name, duration_minutes)`, `appointments.appointment_type_id`.

## Out of scope
Rotating shifts, per-date overrides other than time off.
