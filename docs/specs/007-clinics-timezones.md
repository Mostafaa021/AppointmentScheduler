# 007 — Clinics, time zones, holidays

| Status | Refined ☑ · Designed ☐ · Built ☐ · Tested ☐ · Reviewed ☐ · Released ☐ · Documented ☐ |
|---|---|
| Milestone | M4 |
| Business rules touched | BR-4 |

## Goal
Support several clinics in different time zones, each with its own holidays.

## Acceptance criteria
- **AC-007-1** Clinic has `name`, `address`, `timeZoneId` (IANA, validated with `TimeZoneInfo.FindSystemTimeZoneById`).
- **AC-007-2** A doctor belongs to exactly one clinic; availability and schedules render in that clinic's zone.
- **AC-007-3** On a DST-change date the generated slots are still 09:00–16:30 local; their UTC offsets differ from the previous day.
- **AC-007-4** `POST /clinics/{id}/holidays {date, name}` → no slots offered that local date (`Unavailable`).
- **AC-007-5** Appointments created across clinics never interact except via the patient rule (BR-2), which compares UTC ranges.
- **AC-007-6** Responses include `timeZone` so the UI can label times ("Europe/London").

## Data changes
`clinics(id, name, address, time_zone_id)`, `clinic_holidays(clinic_id, date, name)`, `doctors.clinic_id`, `appointments.clinic_id`.

## Out of scope
Per-tenant data isolation (single organization).
