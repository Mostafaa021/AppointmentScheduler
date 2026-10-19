# SPEC — Clinic Appointment Scheduler

Short graded specification. Detail lives in [`docs/specs`](docs/specs) (acceptance criteria) and [`docs/adr`](docs/adr) (decisions).

## 1. Requirement
A receptionist books patients with doctors and manages the day's schedule **without ever creating a conflicting appointment**.

| Flow | What the user does | Spec |
|---|---|---|
| Availability | Select doctor + date → see 30-min slots 09:00–17:00 (clinic time) as Free / Booked / Past / PatientBusy | [001](docs/specs/001-availability.md) |
| Book | Choose a free slot, a patient and a reason → review → confirm | [002](docs/specs/002-booking.md) |
| Manage | View the day's schedule, cancel, reschedule to another free slot | [003](docs/specs/003-manage-schedule.md) |

### Business rules
| ID | Rule | Enforced by |
|---|---|---|
| BR-1 | A doctor cannot have two overlapping Scheduled appointments | Service pre-check → **PostgreSQL exclusion constraint** |
| BR-2 | A patient cannot have two overlapping Scheduled appointments (any doctor) | Service pre-check → **PostgreSQL exclusion constraint** |
| BR-3 | No booking or rescheduling into the past | `Appointment` aggregate + injected `TimeProvider` |
| BR-4 | Start must be a slot on the 30-min grid inside opening hours | `DaySchedule.EnsureValidSlot` |
| BR-5 | Only future Scheduled appointments can be cancelled / rescheduled | `Appointment` aggregate |
| BR-7 | A Doctor-only user reads only their own schedule and changes nothing (M2) | Role policies + `ScheduleAccess` in `AppointmentService` |

## 2. Data model (PostgreSQL, snake_case)
```
doctors       (id PK, full_name, specialty)
patients      (id PK, mrn UNIQUE, full_name, date_of_birth, phone)              -- fictional data only
appointments  (id uuid PK (v7), doctor_id FK, patient_id FK,
               start_utc timestamptz, end_utc timestamptz, reason varchar(500),
               status 'Scheduled'|'Cancelled', created_at_utc, cancelled_at_utc, cancellation_reason,
               xmin → optimistic concurrency token)
  CHECK  end_utc > start_utc
  EXCLUDE USING gist (doctor_id  WITH =, tstzrange(start_utc, end_utc, '[)') WITH &&) WHERE status = 'Scheduled'
  EXCLUDE USING gist (patient_id WITH =, tstzrange(start_utc, end_utc, '[)') WITH &&) WHERE status = 'Scheduled'
  INDEX (doctor_id, start_utc), (patient_id, start_utc)
```
Cancel is a soft status change: history is kept and the `WHERE status = 'Scheduled'` makes the slot free again.

## 3. Main APIs (`/api/v1`, JSON, every body is an `ApiResponse<T>` envelope)
ASP.NET Core **controllers** (`Api/Controllers`), authenticated by default with a JWT bearer token (M2, [spec 004](docs/specs/004-auth.md)).
Each action declares every status it can return with `[ProducesResponseType]`, so `/scalar` shows the full contract.

Response envelope ([ADR-0015](docs/adr/0015-api-response-envelope.md)); members that don't apply are omitted:
```jsonc
// 201 POST /appointments
{ "isSuccess": true, "statusCode": 201, "message": "Appointment booked.",
  "data": { "id": "…", "localStart": "10:00", … }, "timestamp": "2026-10-19T14:46:27.99+00:00" }
// 409
{ "isSuccess": false, "statusCode": 409, "message": "The doctor already has an appointment at this time.",
  "errorCode": "DOCTOR_CONFLICT", "traceId": "00-…", "timestamp": "…" }
// 400 adds "errors": { "reason": ["…"] }; an unexpected 500 adds "errorDetails" in Development only
```
Reads without a specific message say "Request completed successfully".
`POST /auth/logout` is the one exception: **204** with no body.

**Versioning** ([ADR-0017](docs/adr/0017-api-versioning.md)): the version is in the URL (`/api/v1/…`); responses carry
`api-supported-versions`. An optional `X-Api-Version` header or `api-version` query must match the URL (else 400
`UNSUPPORTED_API_VERSION`). An unknown URL version such as `/api/v2/…` → 404 `UNSUPPORTED_API_VERSION`.

| Method | Route | Notes |
|---|---|---|
| POST | `/auth/login`, `/auth/refresh`, `/auth/logout` · GET `/me` | 15-min access token + rotating refresh cookie; login/refresh anonymous |
| POST / PUT | `/users`, `/users/{id}/roles` | Admin only |
| GET | `/doctors` | list |
| GET | `/patients?search=&page=&pageSize=` | name contains / exact MRN, paged (≤100) |
| GET | `/doctors/{id}/availability?date=YYYY-MM-DD[&patientId=]` | `{timeZone, slots:[{startUtc,endUtc,localStart,status,appointmentId}]}` |
| GET | `/appointments?doctorId=&date=` | day schedule incl. cancelled |
| GET | `/appointments/{id}` | single appointment |
| POST | `/appointments` | `{doctorId, patientId, startUtc, reason}` → **201** + Location |
| POST | `/appointments/{id}/cancel` | `{reason?}` → 200 |
| POST | `/appointments/{id}/reschedule` | `{startUtc}` → 200 |

| Status | `errorCode` (constants in `Domain/Common/ErrorCodes.cs`) |
|---|---|
| 400 | `VALIDATION_FAILED` (field errors), `BAD_REQUEST` (malformed input) |
| 401 / 403 / 423 | `UNAUTHORIZED`, `INVALID_CREDENTIALS`, `INVALID_REFRESH_TOKEN`, `REFRESH_TOKEN_REUSED` / `FORBIDDEN` / `ACCOUNT_LOCKED` |
| 404 | `DOCTOR_NOT_FOUND`, `PATIENT_NOT_FOUND`, `APPOINTMENT_NOT_FOUND`, `USER_NOT_FOUND`, `ROUTE_NOT_FOUND` (no such URL) |
| 405 | `METHOD_NOT_ALLOWED` |
| 404 / 400 | `UNSUPPORTED_API_VERSION` (unknown URL version / mismatched header or query version) |
| 415 | `UNSUPPORTED_MEDIA_TYPE` (body is not JSON) |
| 409 | `DOCTOR_CONFLICT`, `PATIENT_CONFLICT`, `CONCURRENCY_CONFLICT`, `EMAIL_TAKEN` |
| 422 | `APPOINTMENT_IN_PAST`, `INVALID_SLOT`, `APPOINTMENT_NOT_MODIFIABLE` |
| 500 | `INTERNAL_ERROR` — generic message only; details go to the server log with the `traceId` |

Expected failures are `SchedulerException` subclasses that carry their own `ErrorCode`, `StatusCode` and a log-only
`LogDetail` (ids only). All of them become HTTP responses in one place, `Api/Errors/GlobalExceptionHandler.cs`; anything else
→ logged 500. JWT 401/403 and bodiless statuses (unknown route) are written by `ApiErrorWriter` in the same shape.

Interactive docs: `http://localhost:5080/scalar` (Development).

## 4. Where conflict checking lives — and why (judgment test)
**All three layers, each with a different job** ([ADR-0002](docs/adr/0002-postgres-exclusion-constraints.md)):

1. **UI** shows only Free slots and disables the rest. Pure UX — it is stale the moment it renders and is never trusted.
2. **API (Application service)** runs an overlap query before saving so the user gets a precise `409 DOCTOR_CONFLICT` or
   `PATIENT_CONFLICT`. It cannot be the guarantee: two requests can both pass the check before either commits.
3. **Database** is the source of truth: two `EXCLUDE USING gist` constraints over `tstzrange(start, end, '[)')`. PostgreSQL
   rejects any overlapping Scheduled row atomically; the violation (SQLSTATE `23P01`) is translated to the same 409 codes.
   Range-based (not a unique index on start time), so it stays correct if durations vary; half-open `[)` lets 09:00–09:30
   and 09:30–10:00 coexist.

## 5. Concurrent bookings of the same slot
Both requests pass the pre-check; the first `INSERT` commits; the second violates the exclusion constraint and gets `409`.
Proven by `BookingEndpointTests.AC_002_8` (10 parallel requests → exactly one 201) and `ExclusionConstraintTests` (writes that
bypass the service are still rejected). Updates (cancel vs reschedule of the same appointment) use PostgreSQL `xmin` as an
optimistic concurrency token → `409 CONCURRENCY_CONFLICT` instead of a lost update ([ADR-0004](docs/adr/0004-concurrency-etag.md)).

## 6. Time zones
([ADR-0003](docs/adr/0003-time-zones.md))
- Instants are stored as UTC `timestamptz` only.
- "09:00–17:00" is clinic wall-clock time in a configured IANA zone (`Clinic:TimeZone`, default `Europe/London`, which has DST).
  Slots are generated in local time and converted to UTC on the server; DST gap times are skipped.
- The API returns `startUtc` plus `localStart` and `timeZone`; the UI displays `localStart` and posts back `startUtc`, so the
  receptionist's browser zone never matters. Day boundaries (`?date=`) are clinic-local days.
- "Now" comes from an injected `TimeProvider`, so past/future rules are deterministic in tests (`FakeTimeProvider`).

## 7. Architecture
Clean Architecture ([ADR-0001](docs/adr/0001-clean-architecture.md)): `Domain` (rules + repository / unit-of-work
contracts) ← `Application` (use cases, no EF Core) ← `Infrastructure` (EF Core/Npgsql repositories, unit of work, constraint →
error translation, seed; [ADR-0016](docs/adr/0016-repository-unit-of-work.md)) ← `Api` (thin controllers, global exception
handler, auth; responses are `ApiResponse<T>`, [ADR-0015](docs/adr/0015-api-response-envelope.md)). The Api is the only host: background jobs run in it via **Hangfire** ([ADR-0014](docs/adr/0014-hangfire-background-jobs.md),
PostgreSQL storage, hourly expired-refresh-token purge). Layer rules are enforced by `ArchitectureTests`. Frontend: Angular 22 +
Material, feature folders `features/schedule` and `features/auth`.

## 8. Beyond the brief (roadmap, specs written)
The repository is set up as a production simulation; these are specified and planned as later milestones
([backlog](docs/backlog.md)): authentication/roles (004 — **built in M2**), patient CRUD (005), working hours/breaks/time off/appointment types
(006), multiple clinics & time zones (007), notifications via Wolverine + RabbitMQ outbox (008), idempotency keys (009),
rate limiting, resilience, OpenTelemetry → Jaeger, k6 load tests. Non-functional targets: [000-nfr](docs/specs/000-nfr.md).

## 9. Out of scope (for the challenge milestone)
Authentication, doctor-specific hours, multi-clinic, patient editing, notifications — see §8.
