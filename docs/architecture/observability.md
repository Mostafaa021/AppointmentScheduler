# Observability ✅ (traces) · 🟡 (logs, metrics endpoint)

Find a slow or failing request without guessing. NFR-8 in
[non-functional.md](../requirements/non-functional.md#nfr-8--observability) · [ADR-0010](../adr/0010-observability-otel-jaeger.md).

## Components
| Piece | Job |
|---|---|
| `Api/Configuration/ObservabilityConfig.cs` | OpenTelemetry: resource `appointment-scheduler-api` + `deployment.environment.name`; tracing for ASP.NET Core (health and `/hangfire` filtered out; `enduser.id` = caller's id only), HttpClient, EF Core and the `Wolverine` source; metrics for ASP.NET Core, HttpClient and the `AppointmentScheduler` meter |
| `Observability:OtlpEndpoint` | traces target; Development / Docker → Jaeger `:4317`; empty = no export |
| `Observability:MetricsEndpoint` | metrics target (Jaeger has no metrics); empty = no export |
| `RateLimitMetrics` | `scheduler.rate_limit.rejected{policy}` |
| `ApiResponse.traceId` | the same trace id the client sees in error bodies |

## Signal flow
```mermaid
flowchart LR
  subgraph api[api process]
    I1[ASP.NET Core spans] --> SDK[OpenTelemetry SDK]
    I2[EF Core spans — SQL] --> SDK
    I3[HttpClient spans] --> SDK
    I4[Wolverine spans] --> SDK
    M1[request + rate-limit metrics] --> SDK
  end
  SDK -->|OTLP gRPC traces| J[Jaeger :4317 → UI :16686]
  SDK -.->|OTLP metrics, when configured| G[Metrics backend<br/>Grafana / SigNoz — not in compose]
  E[ApiResponse error<br/>traceId 00-…] -. same id .-> J
```

## One request, one trace
```mermaid
sequenceDiagram
  participant UI as Browser
  participant API as api (span: POST /api/v1/appointments)
  participant EF as EF Core (child spans)
  participant DB as PostgreSQL
  participant J as Jaeger
  UI->>API: POST /appointments
  API->>EF: pre-check queries
  EF->>DB: SELECT … (span with the SQL text, no parameter values)
  API->>EF: SaveChanges
  EF->>DB: INSERT appointments, audit_records
  API-->>UI: 201 (or an error with traceId)
  API--)J: export spans (batched)
```
Look a request up in Jaeger by service `appointment-scheduler-api`, or paste an error's `traceId`.

**Still to do (S-60):** structured JSON logs (Serilog) carrying `TraceId`, a `/metrics` endpoint, and `/health/ready`
(database + broker).
