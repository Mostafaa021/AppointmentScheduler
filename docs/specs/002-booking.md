# 002 — Book an appointment

| Status | Refined ☑ · Designed ☑ · Built ☑ · Tested ☑ · Reviewed ☐ · Released ☐ · Documented ☑ (M1 scope) |
|---|---|
| Milestone | M1 |
| Business rules touched | BR-1, BR-2, BR-3, BR-4 |

## Goal
Book a patient into a free slot with a reason, guaranteed conflict-free even under concurrent requests.

## User story
As a receptionist, I want to choose a patient, a free slot and a reason and confirm, so that the visit is on the doctor's schedule.

## Acceptance criteria
- **AC-002-1** Given a free future slot, When I POST a booking with doctor, patient, `startUtc` and reason, Then 201 with the
  appointment and a `Location` header; status is `Scheduled`; `endUtc = startUtc + 30 min` (M1) / type duration (M4).
- **AC-002-2** (BR-1) Given the doctor already has an overlapping Scheduled appointment, Then 409 `DOCTOR_CONFLICT`.
- **AC-002-3** (BR-2) Given the patient already has an overlapping Scheduled appointment with any doctor, Then 409
  `PATIENT_CONFLICT`.
- **AC-002-4** (BR-3) Given `startUtc` is in the past, Then 422 `APPOINTMENT_IN_PAST`.
- **AC-002-5** (BR-4) Given `startUtc` is off the 30-min grid or outside working hours, Then 422 `INVALID_SLOT`.
- **AC-002-6** Reason is required, trimmed, 1–500 chars → else 400 validation problem.
- **AC-002-7** Unknown doctor/patient → 404.
- **AC-002-8** (NFR-1) Given N parallel requests for the same doctor+slot, Then exactly one returns 201 and the others 409.

## API contract
| Method | Route | Request | Responses |
|---|---|---|---|
| POST | `/api/v1/appointments` | `{doctorId, patientId, startUtc, reason}` (+ M4 `appointmentTypeId`), header `Idempotency-Key` (M3) | 201 · 400 · 404 · 409 · 422 |

## Design
Domain `Appointment.Schedule(...)` enforces BR-3/4 → `AppointmentService.BookAsync` pre-checks BR-1/2 for specific error codes →
`SaveChanges`; Postgres exclusion constraints reject races (`23P01` → `ConflictException`). See ADR-0002, ADR-0004.

## Test plan
| AC | Test |
|---|---|
| AC-002-4, 5 | UnitTests › AppointmentTests |
| AC-002-1..3, 6..8 | IntegrationTests › BookingEndpointTests, ExclusionConstraintTests |

## Out of scope
Recurring appointments, overbooking overrides.
