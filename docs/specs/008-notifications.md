# 008 — Notifications (messaging)

| Status | Refined ☑ · Designed ☑ · Built ☐ · Tested ☐ · Reviewed ☐ · Released ☐ · Documented ☐ |
|---|---|
| Milestone | M5 |

## Goal
Patients get an e-mail when an appointment is booked, cancelled or rescheduled, and a reminder 24 h before — without slowing
down or endangering the booking transaction.

## Acceptance criteria
- **AC-008-1** Booking/cancel/reschedule raises a domain event saved in the **same transaction** as the appointment (Wolverine
  EF Core outbox); if the transaction rolls back, no message is sent.
- **AC-008-2** In-process Wolverine handlers consume events from RabbitMQ and enqueue a Hangfire job that sends the e-mail via SMTP within 10 s; in Development a logging sender records metadata only (no PII, no real e-mail).
- **AC-008-3** On booking/reschedule a reminder is scheduled for `startUtc − 24h` (skipped if that is already past); on
  cancel/reschedule the reminder handler re-reads the appointment and does nothing if it is cancelled or moved.
- **AC-008-4** SMTP failures are retried with exponential backoff + jitter; after 5 consecutive failures the circuit opens for 30 s.
- **AC-008-5** A message failing all retries goes to the dead-letter queue and a `notification_log` row with `Failed` status.
- **AC-008-6** Handlers are idempotent: redelivery of the same message id never sends a second e-mail (durable inbox).
- **AC-008-7** The full flow is one trace in Jaeger (API span → outbox → RabbitMQ → handler → Hangfire job → SMTP).

## Data changes
`notification_log(id, appointment_id, type, recipient_hash, status, attempts, last_error, sent_at_utc)`; Wolverine tables in schema `wolverine`.

## Design
ADR-0007 (Wolverine + RabbitMQ + outbox), ADR-0008 (resilience).

## Out of scope
SMS, user notification preferences, templates editor.
