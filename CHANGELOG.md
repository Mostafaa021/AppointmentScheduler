# Changelog

Format: [Keep a Changelog](https://keepachangelog.com). Versions follow milestones.

## [Unreleased]

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
