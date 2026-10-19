# 003 — Manage the day's schedule

| Status | Refined ☑ · Designed ☑ · Built ☑ · Tested ☑ · Reviewed ☐ · Released ☐ · Documented ☑ (M1 scope) |
|---|---|
| Milestone | M1 (ETag/If-Match in M3, roles in M2) |
| Business rules touched | BR-1, BR-2, BR-3, BR-5, BR-7 |

## Goal
View a doctor's appointments for a day, cancel one, or move it to another free slot.

## Acceptance criteria
- **AC-003-1** GET day schedule for doctor+date returns appointments ordered by start, with patient name, reason, status and
  clinic-local times; cancelled ones are included with status `Cancelled`.
- **AC-003-2** Cancel a future Scheduled appointment → 200, status `Cancelled`, slot becomes Free (AC-001-3).
- **AC-003-3** (BR-5) Cancel/reschedule a Cancelled or already-started appointment → 422 `appointment_not_modifiable`.
- **AC-003-4** Reschedule to a free future slot (same doctor) → 200 with new times; old slot becomes Free.
- **AC-003-5** Reschedule into a conflicting slot → 409 `doctor_conflict` / `patient_conflict`; the appointment's own current
  slot never counts as a conflict.
- **AC-003-6** Reschedule into the past / an invalid slot → 422.
- **AC-003-7** Unknown appointment → 404.
- **AC-003-8** (M3) Cancel/reschedule accept `If-Match: "<etag>"`; stale ETag → 412 `precondition_failed`; concurrent updates
  never silently overwrite each other (xmin token → 409 `concurrency_conflict`).
- **AC-003-9** (M2) A user in role Doctor can read only their own schedule (403 otherwise) and cannot cancel/reschedule.
  Detailed in spec 004: AC-004-9 (resource check), AC-004-8 (403 `forbidden` on cancel/reschedule), AC-004-17 (Doctor UI).

## API contract
| Method | Route | Request | Responses |
|---|---|---|---|
| GET | `/api/v1/appointments` | `doctorId`, `date` | 200 `[AppointmentDto]` |
| GET | `/api/v1/appointments/{id}` | — | 200 + `ETag` · 404 |
| POST | `/api/v1/appointments/{id}/cancel` | `{reason?}` | 200 · 404 · 409 · 412 · 422 |
| POST | `/api/v1/appointments/{id}/reschedule` | `{startUtc}` | 200 · 404 · 409 · 412 · 422 |

## Test plan
| AC | Test |
|---|---|
| AC-003-3, 6 | UnitTests › AppointmentTests |
| AC-003-1..8 | IntegrationTests › ManageScheduleEndpointTests |

## Out of scope
Moving an appointment to a different doctor (book new + cancel old instead).
