# ADR-0007 — Messaging: Wolverine with RabbitMQ and a durable PostgreSQL outbox

- Status: Accepted · Date: 2026-10-19

## Context
Notifications/reminders must not slow down or break bookings, and must never be sent for a booking that rolled back (dual-write
problem).

## Decision
- Aggregates raise domain events (`AppointmentBooked`, `AppointmentCancelled`, `AppointmentRescheduled`).
- Infrastructure publishes them through **Wolverine's EF Core transactional outbox** (message rows committed in the same Postgres
  transaction), then relays them to **RabbitMQ**.
- The **Worker** host consumes with a durable inbox (dedupe), sends e-mail, schedules reminders with Wolverine scheduled messages.
- Error policy: retry with cooldown (1 s, 5 s, 30 s) → move to **dead-letter queue**; ops runbook explains replay.

## Alternatives
Raw RabbitMQ.Client + hand-written outbox (more code), MassTransit (commercial licensing from v9), in-process background queue
(loses messages on crash).

## Consequences
+ Atomic state change + message, at-least-once delivery, idempotent handlers, traceable end to end.
− Extra infrastructure (RabbitMQ) and Wolverine tables; acceptable for the production simulation.
