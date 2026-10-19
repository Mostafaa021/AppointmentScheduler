# Functional requirements

This is the catalogue of what the system does, one row per requirement. It sits on top of the feature specs: each row says
what is required in plain words, its status, where it lives in the code, and how it is verified.
- The **acceptance criteria** (Given/When/Then, exact status codes) stay in [`docs/specs`](../specs).
- **Non-functional** requirements are in [non-functional.md](non-functional.md).
- **Diagrams** are per feature in [`docs/architecture`](../architecture).

**Status**
- ✅ built;
- 🟡 partly built;
- 📋 planned, with the spec written and nothing in code yet.

**Verified by**
- a unit or architecture test (`tests/`);
- or **live**: checked against the running API with `curl` or the browser. There are no integration tests by design
  ([plan 011](../../.claude/plans/011-unit-and-architecture-tests-only.md)).

**Roles:** **R** = Receptionist, **D** = Doctor (only their own schedule, read-only), **A** = Admin. All `/api/v1` routes need a
signed-in user unless stated.

Paths are relative to `src/AppointmentScheduler.*` (e.g. `Domain/…`) or the repository root (`web/…`).

## Business rules
| ID | Rule | Enforced by | Verified by |
|---|---|---|---|
| BR-1 | A doctor cannot have two overlapping *Scheduled* appointments | `AppointmentService` pre-check → PostgreSQL exclusion constraint `ex_appointments_doctor_overlap` | `AppointmentServiceTests.AC_002_2…`; live: parallel booking → one 201, others 409 |
| BR-2 | A patient cannot have two overlapping *Scheduled* appointments, with any doctor | Same, `ex_appointments_patient_overlap` | live (409 `PATIENT_CONFLICT`) |
| BR-3 | No booking or rescheduling into the past | `Domain/Appointments/Appointment.cs` + injected `TimeProvider` | `AppointmentTests.AC_002_4…`, `AC_003_6…` |
| BR-4 | A start must be a slot on the 30-minute grid inside opening hours (clinic-local) | `Domain/Scheduling/DaySchedule.cs` (`EnsureValidSlot`) | `AppointmentTests.AC_002_5…`, `DayScheduleTests.BR_4…` |
| BR-5 | Only future *Scheduled* appointments can be cancelled or rescheduled | `Appointment.Cancel` / `Reschedule` | `AppointmentTests.AC_003_3…` |
| BR-6 📋 | A patient with future appointments can't be deactivated | spec 005 | — |
| BR-7 | A Doctor-only user reads only their own schedule and changes nothing | role policies (`Api/Auth/Policies.cs`) + `Application/Appointments/ScheduleAccess.cs` | `ScheduleAccessTests.AC_004_9…`, `AC_004_8…` |

## 001 — Availability ✅ ([spec](../specs/001-availability.md) · [architecture](../architecture/availability.md))
| ID | Requirement | Roles | Status | API | Implemented in | Verified by | ACs |
|---|---|---|---|---|---|---|---|
| FR-001-1 | Show a doctor's day as 16 half-hour slots, 09:00–16:30 clinic time, in order | R, D (own), A | ✅ | `GET /doctors/{id}/availability?date=` | `Domain/Scheduling/DaySchedule.cs`, `Application/Availability/AvailabilityService.cs`, `Api/Controllers/DoctorsController.cs` | `DayScheduleTests.AC_001_1…` | AC-001-1 |
| FR-001-2 | Mark each slot **Booked** (with the appointment id) when a *Scheduled* appointment covers it; a cancelled appointment frees its slot | R, D, A | ✅ | same | `AvailabilityService` (half-open overlap), `AppointmentRepository.ListScheduledForDoctorAsync` | live | AC-001-2, AC-001-3 |
| FR-001-3 | Mark slots that already started as **Past** (not bookable) | R, D, A | ✅ | same | `AvailabilityService` + `TimeProvider` | live | AC-001-4 |
| FR-001-4 | With `patientId`, mark slots where that patient is busy with any doctor as **PatientBusy** | R, A | ✅ | `…&patientId=` | `AppointmentRepository.ListScheduledForPatientAsync` | live | AC-001-5 |
| FR-001-5 | Return each slot as UTC start/end plus the clinic-local `HH:mm`, so the browser never converts zones (correct across DST) | all | ✅ | same | `DaySchedule.SlotsFor`, `ClinicOptions` | `DayScheduleTests.AC_001_6…` | AC-001-6 |
| FR-001-6 | Unknown doctor → 404 `DOCTOR_NOT_FOUND`; a malformed date → 400 | all | ✅ | same | `IRepository.ExistsAsync`, MVC binding → `BadRequestResponse` | live | AC-001-7 |
| FR-001-7 | Slots follow the doctor's working hours, breaks, time off, holidays and appointment type length | — | 📋 | `…&appointmentTypeId=` | spec 006/007 | — | AC-001-8 |
| FR-001-8 | The schedule opens on the **clinic's** today (and **Today** returns to it), not the browser's date: the clinic zone decides the day | all | ✅ | `GET /clinic` | `Application/Clinics/ClinicQueries.cs`, `Api/Controllers/ClinicController.cs`, `web/…/schedule-page.ts`, `doctor-schedule-page.ts` | `ClinicQueriesTests`; live (browser opens on the clinic date, **Today** returns to it) | AC-001-6, ADR-0003 |
| UI | Pick doctor and date; a slot grid colours Free/Booked/Past/PatientBusy; only Free slots are clickable | R | ✅ | — | `web/src/app/features/schedule/schedule-page.ts`, `slot-grid.ts` | live (browser) | — |

## 002 — Booking ✅ ([spec](../specs/002-booking.md) · [architecture](../architecture/booking.md))
| ID | Requirement | Roles | Status | API | Implemented in | Verified by | ACs |
|---|---|---|---|---|---|---|---|
| FR-002-1 | Book a free future slot for a patient with a reason → 201 + `Location`, status *Scheduled*, end = start + 30 min | R, A | ✅ | `POST /appointments` (+ `Idempotency-Key`) | `Appointment.Schedule`, `AppointmentService.BookAsync`, `AppointmentsController` | `AppointmentTests.AC_002_1…` | AC-002-1 |
| FR-002-2 | Reject a slot the doctor already has → 409 `DOCTOR_CONFLICT` (BR-1) | R, A | ✅ | same | pre-check `DoctorHasOverlapAsync` + exclusion constraint (23P01 → same code in `AppDbContext`) | `AppointmentServiceTests.AC_002_2…`; live | AC-002-2 |
| FR-002-3 | Reject a time the patient is already booked with any doctor → 409 `PATIENT_CONFLICT` (BR-2) | R, A | ✅ | same | `PatientHasOverlapAsync` + constraint | live | AC-002-3 |
| FR-002-4 | Reject past starts (422 `APPOINTMENT_IN_PAST`) and starts off the grid or outside hours (422 `INVALID_SLOT`) | R, A | ✅ | same | `Appointment`, `DaySchedule` | `AppointmentTests.AC_002_4…`, `AC_002_5…` | AC-002-4, AC-002-5 |
| FR-002-5 | Require a reason, trimmed, 1–500 characters → else 400 `VALIDATION_FAILED` with field errors | R, A | ✅ | same | `Application/Appointments/AppointmentValidators.cs` | `AppointmentValidatorTests.AC_002_6…` | AC-002-6 |
| FR-002-6 | Unknown doctor or patient → 404, and nothing is saved | R, A | ✅ | same | `AppointmentService.EnsureExistsAsync` | `AppointmentServiceTests.AC_002_7…` | AC-002-7 |
| FR-002-7 | N parallel requests for one slot → exactly one 201, the rest a fast, precise 409 (no deadlock) | R, A | ✅ | same | ordered advisory locks + pre-check in one transaction (`AppointmentRepository.LockScheduleAsync`, `IUnitOfWork.ExecuteInTransactionAsync`); exclusion constraint as the guarantee (ADR-0002) | `AppointmentServiceTests.AC_002_8…` (lock before pre-check); live: SPEC §5 "See it yourself" → 1×201, 7×409 | AC-002-8 |
| FR-002-8 | The booking and its audit row commit together, or neither does | — | ✅ | — | one `IUnitOfWork.SaveChangesAsync` (ADR-0011, ADR-0016) | `AppointmentServiceTests` (no commit on rejection) | AC-004-24 |
| UI | Free slot → patient search → reason → **Review** → **Confirm**; the confirm button is disabled while sending | R | ✅ | — | `web/src/app/features/schedule/book-dialog.ts` | — | — |

## 003 — Manage the day's schedule ✅ ([spec](../specs/003-manage-schedule.md) · [architecture](../architecture/manage-schedule.md))
| ID | Requirement | Roles | Status | API | Implemented in | Verified by | ACs |
|---|---|---|---|---|---|---|---|
| FR-003-1 | List a doctor's appointments for a clinic-local day, ordered by start, with patient, reason, status and local times; cancelled ones included | R, D (own), A | ✅ | `GET /appointments?doctorId=&date=` | `AppointmentService.GetDayScheduleAsync`, `AppointmentRepository.ListDayScheduleAsync` | live | AC-003-1 |
| FR-003-2 | Read one appointment | R, D (own), A | ✅ | `GET /appointments/{id}` | `AppointmentService.GetAsync` | live | — |
| FR-003-3 | Cancel a future *Scheduled* appointment (optional reason ≤ 500); its slot becomes free | R, A | ✅ | `POST /appointments/{id}/cancel` | `Appointment.Cancel` | `AppointmentTests.AC_003_2…` | AC-003-2 |
| FR-003-4 | Reschedule to a free future slot of the same doctor; the old slot becomes free; its own current slot never counts as a conflict | R, A | ✅ | `POST /appointments/{id}/reschedule` | `Appointment.Reschedule`, `OthersOverlapping` | `AppointmentTests.AC_003_4…` | AC-003-4, AC-003-5 |
| FR-003-5 | Cancelled or already-started appointments can't change → 422 `APPOINTMENT_NOT_MODIFIABLE` (BR-5) | R, A | ✅ | both | `Appointment` | `AppointmentTests.AC_003_3…` | AC-003-3 |
| FR-003-6 | Reschedule into the past or an invalid slot → 422; into a conflict → 409 | R, A | ✅ | reschedule | `Appointment`, pre-check + constraint | `AppointmentTests.AC_003_6…`; live | AC-003-5, AC-003-6 |
| FR-003-7 | Unknown appointment → 404 `APPOINTMENT_NOT_FOUND` | all | ✅ | all three | `AppointmentService.FindAsync` | live | AC-003-7 |
| FR-003-8 | Two concurrent changes to one appointment never silently overwrite each other → 409 `CONCURRENCY_CONFLICT` | R, A | ✅ | cancel, reschedule | PostgreSQL `xmin` token (ADR-0004) | live | AC-003-8 (part) |
| FR-003-9 | `ETag` / `If-Match` on cancel and reschedule → 412 on a stale ETag | R, A | 📋 set aside (S-31) | — | — | — | AC-003-8 |
| UI | Day schedule with **Reschedule** (busy slots marked) and **Cancel** dialogs; a Doctor sees only their own day, read-only | R, D | ✅ | — | `doctor-schedule-page.ts`, `reschedule-dialog.ts`, `cancel-dialog.ts` | live (browser, as Receptionist and as Doctor) | AC-004-17 |

## 004 — Authentication, users and audit ✅ ([spec](../specs/004-auth.md) · [architecture](../architecture/auth-and-users.md))
| ID | Requirement | Roles | Status | API | Implemented in | Verified by | ACs |
|---|---|---|---|---|---|---|---|
| FR-004-1 | Sign in with e-mail and password → 15-minute access token + rotating refresh cookie (HttpOnly, Secure, SameSite=Strict, path `/api/v1/auth`) | anonymous | ✅ | `POST /auth/login` | `Infrastructure/Auth/IdentityAuthService.cs`, `TokenIssuer.cs`, `Api/Auth/RefreshTokenCookie.cs` | `AuthValidatorTests.AC_004_1…`; live | AC-004-1, AC-004-4 |
| FR-004-2 | Wrong e-mail or password → 401 `INVALID_CREDENTIALS` with one fixed message (no user enumeration) | anonymous | ✅ | same | `IdentityAuthService` | live | AC-004-2 |
| FR-004-3 | Lock the account for 15 minutes after 5 consecutive failures → 423 `ACCOUNT_LOCKED` | anonymous | ✅ | same | atomic attempt reservation in `IdentityAuthService` | live | AC-004-3 |
| FR-004-4 | Refresh rotates the token; presenting an already-rotated token revokes the whole family → 401 `REFRESH_TOKEN_REUSED` | anonymous (cookie) | ✅ | `POST /auth/refresh` | `IdentityAuthService` (explicit transaction) | live | AC-004-5, AC-004-6 |
| FR-004-5 | Log out: revoke the refresh token and clear the cookie → 204 | any | ✅ | `POST /auth/logout` | `AuthController` | live | AC-004-7 |
| FR-004-6 | Return the caller's id, e-mail, roles and doctor link | any | ✅ | `GET /me` | `AuthController`, `Api/Auth/HttpCurrentUser.cs` | live | AC-004-10 |
| FR-004-7 | Role matrix: R and A manage the schedule; D reads only (403 `FORBIDDEN` on changes); user admin is A only | all | ✅ | all | `Api/Auth/Policies.cs`, `ApiControllerBase` | `ScheduleAccessTests.AC_004_8…`; architecture tests | AC-004-8 |
| FR-004-8 | A Doctor-only user reads only their own doctor's schedule and appointments (BR-7); with no doctor link it fails closed | D | ✅ | appointment and availability reads | `ScheduleAccess.EnsureCanRead` | `ScheduleAccessTests.AC_004_9…` | AC-004-9 |
| FR-004-9 | An Admin creates users with e-mail, password (policy), roles and optional doctor link → 201; duplicate e-mail → 409 `EMAIL_TAKEN` | A | ✅ | `POST /users` (+ `Idempotency-Key`) | `Infrastructure/Auth/IdentityUserAdminService.cs`, `Application/Auth/AuthValidators.cs`, `PasswordPolicy.cs` | `AuthValidatorTests.AC_004_11…`, `AC_004_12…` | AC-004-11…13 |
| FR-004-10 | An Admin replaces a user's roles and doctor link; applies at their next login or refresh | A | ✅ | `PUT /users/{id}/roles` | `IdentityUserAdminService` | `AuthValidatorTests.AC_004_14…` | AC-004-14 |
| FR-004-11 | Validation failures return 400 `VALIDATION_FAILED` with every failing field at once (camelCase names) | all | ✅ | all writes | `Application/Common/ValidationExtensions.cs` | `ValidationExtensionsTests.AC_004_22…`, `AppointmentValidatorTests.AC_004_22…` | AC-004-22 |
| FR-004-12 | Audit: login success/failure, logout, token reuse, booking, cancel, reschedule, user created, roles changed — one row each, ids only, committed with the change | — | ✅ | — | `Application/Common/AuditLog.cs`, `audit_records` (ADR-0011) | `AppointmentServiceTests` (no audit on rejection); live SQL | AC-004-24, AC-004-25 |
| FR-004-13 | Development seeds three fictional users (Admin, Receptionist, Doctor) | — | ✅ | — | `Infrastructure/Persistence/Seed/DbSeeder.cs` | live | AC-004-26 |
| UI | Login page; the token is kept in memory only; one silent refresh for all pending 401s, then retry once; log out always visible | all | ✅ | — | `web/src/app/features/auth/login-page.ts`, `core/auth/auth.interceptor.ts`, `auth.service.ts` | live (browser: sign in, token refresh, log out) | AC-004-15…21 |

## 005 — Patients 🟡 ([spec](../specs/005-patients.md) · [architecture](../architecture/lookups.md))
| ID | Requirement | Roles | Status | API | Implemented in | Verified by | ACs |
|---|---|---|---|---|---|---|---|
| FR-005-1 | Search patients by part of the name (case-insensitive) or exact MRN, paged (≤ 100) → `{items, page, pageSize, total}` | R, A | ✅ (without `includeInactive`) | `GET /patients?search=&page=&pageSize=` | `LookupQueries.SearchPatientsAsync`, `PatientRepository.SearchAsync` | live | AC-005-1 |
| FR-005-2 | List doctors by name | all | ✅ | `GET /doctors` | `LookupQueries.ListDoctorsAsync` | live | — |
| FR-005-3 | Create a patient with a server-generated MRN | R, A | 📋 | `POST /patients` | — | — | AC-005-2 |
| FR-005-4 | Update a patient's contact details | R, A | 📋 | `PUT /patients/{id}` | — | — | AC-005-3 |
| FR-005-5 | Deactivate a patient (blocked while they have future appointments, BR-6); inactive patients can't be booked | R, A | 📋 | `POST /patients/{id}/deactivate` | — | — | AC-005-4 |

## 006 — Doctors, working hours, time off, appointment types 📋 ([spec](../specs/006-doctor-schedules.md) · [architecture](../architecture/doctor-schedules.md))
| ID | Requirement | Status | ACs |
|---|---|---|---|
| FR-006-1 | Admin CRUD for doctors (name, specialty, clinic, slot size 15/30/60, active) | 📋 | AC-006-1 |
| FR-006-2 | A weekly working-hours template per doctor; the gaps between ranges are breaks | 📋 | AC-006-2 |
| FR-006-3 | Time off; overlapping appointments are listed as warnings, never auto-cancelled | 📋 | AC-006-3 |
| FR-006-4 | Appointment types with durations that are multiples of the slot size | 📋 | AC-006-4 |
| FR-006-5 | The default seed keeps today's behaviour (Mon–Fri 09:00–17:00, 30 min) | 📋 | AC-006-5 |
| FR-006-6 | Changing hours never deletes appointments; those now outside hours are flagged | 📋 | AC-006-6 |

Today every doctor works the clinic hours from `Clinic:Open`/`Close`/`SlotMinutes`.

## 007 — Clinics, time zones, holidays 📋 ([spec](../specs/007-clinics-timezones.md) · [architecture](../architecture/clinics-timezones.md))
| ID | Requirement | Status | ACs |
|---|---|---|---|
| FR-007-1 | Clinics with name, address and a validated IANA time zone; each doctor belongs to one | 📋 | AC-007-1, AC-007-2 |
| FR-007-2 | Slots stay 09:00–16:30 local on DST-change dates | ✅ for the single configured clinic (`DayScheduleTests.AC_001_6…`) | AC-007-3 |
| FR-007-3 | Clinic holidays offer no slots (`Unavailable`) | 📋 | AC-007-4 |
| FR-007-4 | Appointments in different clinics interact only through BR-2, compared in UTC | ✅ by design (UTC ranges) | AC-007-5 |
| FR-007-5 | Responses carry `timeZone` so the UI can label times | ✅ (availability, `GET /clinic`) | AC-007-6 |

Today there is one clinic, configured by `Clinic:TimeZone` (default `Europe/London`).

## 008 — Notifications 📋 ([spec](../specs/008-notifications.md) · [architecture](../architecture/messaging.md))
| ID | Requirement | Status | ACs |
|---|---|---|---|
| FR-008-1 | Booking, cancel and reschedule raise an event saved in the same transaction (outbox) | 📋 (the Wolverine store and RabbitMQ wiring exist, ADR-0007) | AC-008-1 |
| FR-008-2 | A handler sends the confirmation e-mail within 10 s (a logging sender in Development) | 📋 | AC-008-2 |
| FR-008-3 | A reminder at start − 24 h, which does nothing if the appointment was cancelled or moved | 📋 | AC-008-3 |
| FR-008-4 | SMTP retries with backoff; the circuit opens after 5 failures | 📋 | AC-008-4 |
| FR-008-5 | Failed messages go to a dead-letter queue and `notification_log` | 📋 | AC-008-5 |
| FR-008-6 | Redelivery never sends twice (durable inbox) | 📋 | AC-008-6 |
| FR-008-7 | One trace from API to SMTP | 📋 | AC-008-7 |

## 009 — Idempotent writes ✅ ([spec](../specs/009-idempotency.md) · [architecture](../architecture/idempotency.md))
| ID | Requirement | Roles | Status | API | Implemented in | Verified by | ACs |
|---|---|---|---|---|---|---|---|
| FR-009-1 | Every POST except auth and read-only POSTs (`[SkipIdempotency]`) needs `Idempotency-Key` (1–100 chars) → else 400 `IDEMPOTENCY_KEY_REQUIRED`; PUT/PATCH/DELETE need none | all | ✅ | all POSTs | `Api/Idempotency/IdempotencyFilter.cs` (`Applies`) | `IdempotencyScopeTests`; live | AC-009-1 |
| FR-009-2 | Repeating the same request with the same key replays the first response byte-for-byte with `Idempotent-Replayed: true`; the handler doesn't run again | all | ✅ | same | `IdempotencyFilter`, `RequestFingerprint`, `Infrastructure/Idempotency/EfIdempotencyStore.cs` | live | AC-009-2, AC-009-3 |
| FR-009-3 | The same key with a different request → 422 `IDEMPOTENCY_KEY_REUSED`; still running → 409 + `Retry-After: 1` | all | ✅ | same | same | live | AC-009-4, AC-009-5 |
| FR-009-4 | 5xx answers aren't stored (the key is released), 4xx answers are | all | ✅ | same | `IdempotencyFilter` | live | AC-009-6 |
| FR-009-5 | Keys live 24 h per user and are purged hourly | — | ✅ | — | `PurgeExpiredIdempotencyRecordsJob` (Hangfire) | live | AC-009-7, AC-009-8 |
| UI | The app adds a fresh key to every POST and resends the same key after a silent token refresh | — | ✅ | — | `web/src/app/core/http/idempotency-key.interceptor.ts` | live (request headers in the browser) | — |
