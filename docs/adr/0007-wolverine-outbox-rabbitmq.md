# ADR-0007 — Messaging: Wolverine with RabbitMQ and a durable PostgreSQL outbox

- Status: Accepted · Date: 2026-10-19

## Context
Notifications/reminders must not slow down or break bookings, and must never be sent for a booking that rolled back (dual-write
problem).

## Decision
- Aggregates raise domain events (`AppointmentBooked`, `AppointmentCancelled`, `AppointmentRescheduled`).
- Infrastructure publishes them through **Wolverine's EF Core transactional outbox** (message rows committed in the same Postgres
  transaction), then relays them to **RabbitMQ**.
- The **Api** host (Wolverine handlers run in-process; no separate Worker — ADR-0014) consumes with a durable inbox (dedupe), sends e-mail, schedules reminders with Wolverine scheduled messages.
- Error policy: retry with cooldown (1 s, 5 s, 30 s) → move to **dead-letter queue**; ops runbook explains replay.

## Status (2026-10-19): wiring done, no messages yet
Implemented in `Infrastructure/Messaging/MessagingSetup.cs` and called from `Api/Configuration/MessagingConfig.cs`:
- **Message store:** `PersistMessagesWithPostgresql` in schema `wolverine` (same database), with
  `UseEntityFrameworkCoreTransactions()`.
- **Handlers:** discovered in Application. Development uses `Durability.Mode = Solo`.
- **Broker probe at startup:** one AMQP connect (`RabbitMqAvailability`). Wolverine cannot attach RabbitMQ to a running host,
  so the mode is decided first:
  - **broker up:** `UseRabbitMq` + `AutoProvision` (we own every broker object) + quorum queues + no request/reply queue, and
    the `Messaging:Publishing:Exchange` topic exchange is declared;
  - **broker down: degraded mode.** HTTP keeps serving. `RabbitMqRecoveryService` logs a warning and re-probes every
    `RecoveryIntervalMinutes`. When the broker is back it stops the host (outside Development) so the orchestrator restarts
    it with the transport.
- **Packages:** `WolverineFx.RuntimeCompilation` is required since Wolverine 6.39 (the runtime compiler is no longer in core).
- **Tests** set `Messaging:RabbitMq:Enabled=false`. The test factory disposes the host before the database, because the
  durability agent polls PostgreSQL until the host stops.

Still open (S-50 / S-51): domain events, `PublishMessage<T>().ToRabbitExchange(...).UseDurableOutbox()`, handlers, and the
retry/dead-letter policy.

## Alternatives
Raw RabbitMQ.Client + hand-written outbox (more code), MassTransit (commercial licensing from v9), in-process background queue
(loses messages on crash).

## Consequences
+ Atomic state change + message, at-least-once delivery, idempotent handlers, traceable end to end.
− Extra infrastructure (RabbitMQ) and Wolverine tables; acceptable for the production simulation.
