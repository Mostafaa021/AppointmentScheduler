# ADR-0010 — Observability: OpenTelemetry → Jaeger, Serilog JSON logs, Prometheus metrics endpoint

- Status: Accepted · Date: 2026-10-19

## Decision
- **Traces:** OpenTelemetry SDK with ASP.NET Core, HttpClient, Npgsql and Wolverine instrumentation, exported via OTLP to
  **Jaeger** (all-in-one, UI on :16686). W3C trace context flows API → RabbitMQ → Worker.
- **Logs:** Serilog, compact JSON to console, enriched with `TraceId`, `SpanId`, `UserId`, `CorrelationId`. No PII (ids only).
- **Metrics:** OTel meters (`scheduler.appointments.booked`, `scheduler.appointments.conflicts`, `scheduler.idempotency.replayed`,
  `scheduler.rate_limit.rejected`) + runtime/ASP.NET metrics, exposed at `/metrics` (Prometheus format) for any scraper.
- **Health:** `/health/live` (process) and `/health/ready` (Postgres, RabbitMQ).

## Consequences
+ One click from a slow request to its SQL and message spans; vendor-neutral (swap Jaeger for Tempo/Datadog via OTLP config).
− No log/metric UI shipped in compose (console logs + `/metrics`); acceptable for local simulation.
