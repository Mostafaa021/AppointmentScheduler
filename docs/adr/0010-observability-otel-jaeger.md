# ADR-0010 — Observability: OpenTelemetry → Jaeger, Serilog JSON logs, Prometheus metrics endpoint

- Status: Accepted · Date: 2026-10-19

## Decision
- **Traces:** OpenTelemetry SDK with ASP.NET Core, HttpClient, Npgsql and Wolverine instrumentation, exported via OTLP to
  **Jaeger** (all-in-one, UI on :16686). W3C trace context flows API → RabbitMQ → in-process Wolverine handler / Hangfire job.
- **Logs:** Serilog, compact JSON to console, enriched with `TraceId`, `SpanId`, `UserId`, `CorrelationId`. No PII (ids only).
- **Metrics:** OTel meters (`scheduler.appointments.booked`, `scheduler.appointments.conflicts`, `scheduler.idempotency.replayed`,
  `scheduler.rate_limit.rejected`) + runtime/ASP.NET metrics, exposed at `/metrics` (Prometheus format) for any scraper.
- **Health:** `/health/live` (process) and `/health/ready` (Postgres, RabbitMQ).

## Status (2026-10-19)
Implemented in `Api/Configuration/ObservabilityConfig.cs`:
- traces: ASP.NET Core (without `/health`, `/hangfire`), HttpClient, EF Core and the `Wolverine` source, sent over OTLP to
  `Observability:OtlpEndpoint`;
- request spans tagged `enduser.id` (id only);
- ASP.NET Core and HttpClient metrics, exported only when `Observability:MetricsEndpoint` is set.

Still open (S-60): Serilog JSON logs, custom `scheduler.*` meters, the Prometheus `/metrics` endpoint and `/health/ready`.
Npgsql-level spans are skipped on purpose, because Hangfire's polling would flood them; EF Core spans cover our queries.

## Consequences
+ One click from a slow request to its SQL and message spans; vendor-neutral (swap Jaeger for Tempo/Datadog via OTLP config).
− No log/metric UI shipped in compose (console logs + `/metrics`); acceptable for local simulation.
