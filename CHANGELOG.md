# Changelog

Format: [Keep a Changelog](https://keepachangelog.com). Versions follow milestones.

## [Unreleased]
### Added
- M2 security: ASP.NET Identity + JWT (15 min) with rotating refresh-token cookie and reuse detection; roles Admin /
  Receptionist / Doctor; Doctor reads only own schedule; FluentValidation; security headers; audit records; Angular login,
  silent refresh, guards and read-only doctor view. Demo users in Development.
- Hangfire background jobs inside the API (PostgreSQL storage, dev dashboard) with an hourly expired-refresh-token purge.
- Global exception handler: unexpected errors return a generic 500 `internal_error` and are logged with the trace id.
### Changed
- API moved from Minimal APIs to controllers (routes, status codes and error codes unchanged).
- **Breaking:** every response body is now an `ApiResponse<T>` envelope (`success`, `statusCode`, `message`, `data` |
  `errorCode`, `errors`, `traceId`) instead of bare DTOs + RFC 7807 ProblemDetails; error codes are UPPER_SNAKE constants in
  `ErrorCodes` (`doctor_conflict` → `DOCTOR_CONFLICT`). Unknown routes / wrong methods answer `ROUTE_NOT_FOUND` /
  `METHOD_NOT_ALLOWED`. Controllers declare every status with `[ProducesResponseType]`. Exceptions carry their own status
  and a log-only `LogDetail` (ADR-0015).
### Removed
- `AppointmentScheduler.Worker` project (the API is the single host) and Mailpit from docker-compose.
### Security
- `Newtonsoft.Json` pinned to 13.0.4 (Hangfire.Core referenced vulnerable 11.0.1, GHSA-5crp-9r3c-p9vr).

## [v1-challenge] — 2026-10-19
### Added
- Availability per doctor and clinic-local date: 30-minute slots 09:00–17:00 (Europe/London) as Free / Booked / Past / PatientBusy.
- Book, cancel (soft, with reason) and reschedule appointments; day schedule including cancelled ones.
- Conflict rules: doctor and patient overlaps rejected by PostgreSQL exclusion constraints (+ friendly pre-check), no past
  bookings, slot grid validation, only future scheduled appointments can change; `xmin` optimistic concurrency.
- RFC 7807 ProblemDetails with stable `code` values; OpenAPI + Scalar UI.
- Fictional seed data (4 doctors, 12 patients, appointments today/tomorrow).
- Angular 22 schedule page with booking (review step), reschedule and cancel dialogs.
- Tests: 20 unit, 3 architecture, 24 integration (Testcontainers, parallel booking race), 11 Angular.

### Foundation (M0)
- Clean Architecture solution, central package management, Docker infra (PostgreSQL, RabbitMQ, Mailpit, Jaeger),
  specs 000–009, ADRs 0001–0010, SDLC role agents and project skills, CI workflow.
