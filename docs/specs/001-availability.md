# 001 — Doctor availability

| Status | Refined ☑ · Designed ☑ · Built ☐ · Tested ☐ · Reviewed ☐ · Released ☐ · Documented ☐ |
|---|---|
| Milestone | M1 (single clinic, fixed 09:00–17:00) → M4 (working hours, breaks, time off, holidays, appointment types) |
| Business rules touched | BR-3, BR-4 |

## Goal
A receptionist picks a doctor and a date and sees every bookable slot for that day with its state.

## User story
As a receptionist, I want to see a doctor's slots for a date, so that I only offer times that can actually be booked.

## Acceptance criteria
- **AC-001-1** Given a doctor and a date, When availability is requested, Then 16 slots of 30 minutes from 09:00 to 16:30
  clinic-local time are returned in order (M1 default hours).
- **AC-001-2** Given a Scheduled appointment covering a slot, Then that slot is `Booked` and carries the `appointmentId`.
- **AC-001-3** Given a cancelled appointment, Then its slot is `Free` again.
- **AC-001-4** Given a slot whose start is before now, Then it is `Past` (not bookable).
- **AC-001-5** Given `patientId` is supplied and the patient has another Scheduled appointment overlapping a slot (any doctor),
  Then that slot is `PatientBusy`.
- **AC-001-6** Each slot returns `startUtc`, `endUtc` (ISO-8601 UTC) and `localStart` (`HH:mm`, clinic zone); the UI never
  converts time zones itself.
- **AC-001-7** Unknown doctor → 404 `doctor_not_found`; malformed date → 400.
- **AC-001-8** (M4) Slots come from the doctor's working-hour ranges for that weekday, exclude breaks, time off and clinic
  holidays, and only offer starts where the selected appointment type's whole duration fits.

## API contract
| Method | Route | Request | Responses |
|---|---|---|---|
| GET | `/api/v1/doctors/{id}/availability` | `date=YYYY-MM-DD`, optional `patientId`, (M4) `appointmentTypeId` | 200 `{doctorId, date, timeZone, slots:[{startUtc,endUtc,localStart,status,appointmentId?}]}` · 400 · 404 |

`status` ∈ `Free | Booked | Past | PatientBusy | Unavailable`.

## Design
- `Domain/Scheduling/AvailabilityPolicy` generates candidate slots in clinic-local time and converts them to UTC with
  `TimeZoneInfo`; DST-invalid local times are skipped.
- `Application/Availability/AvailabilityService` loads Scheduled appointments overlapping that local day's UTC window for the
  doctor (and patient) — one query each — then classifies the slots.

## NFR notes
Read endpoint → `reads` rate-limit policy; roles Admin, Receptionist. No PII in the response beyond ids.

## UI notes
Slot grid: Free = primary button, Booked = patient chip + actions, Past/Unavailable = disabled, PatientBusy = warning style.

## Test plan
| AC | Test |
|---|---|
| AC-001-1, 4, 6 | UnitTests › AvailabilityPolicyTests |
| AC-001-2, 3, 5, 7 | IntegrationTests › AvailabilityEndpointTests |

## Out of scope
Multi-day views, waitlists.
