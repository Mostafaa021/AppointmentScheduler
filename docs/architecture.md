# Architecture

The system overview: containers, deployment, layers, data model, host composition and the request pipeline. Each feature has its
own page with component, sequence and state diagrams:

| Feature | Page | Status |
|---|---|---|
| Availability (spec 001) | [architecture/availability.md](architecture/availability.md) | ✅ |
| Booking (spec 002) | [architecture/booking.md](architecture/booking.md) | ✅ |
| Manage the day's schedule (spec 003) | [architecture/manage-schedule.md](architecture/manage-schedule.md) | ✅ |
| Authentication, users and audit (spec 004) | [architecture/auth-and-users.md](architecture/auth-and-users.md) | ✅ |
| Doctor and patient lookups (spec 005) | [architecture/lookups.md](architecture/lookups.md) | 🟡 search built, CRUD planned |
| Idempotent writes (spec 009) | [architecture/idempotency.md](architecture/idempotency.md) | ✅ |
| Rate limiting (ADR-0009) | [architecture/rate-limiting.md](architecture/rate-limiting.md) | ✅ |
| Errors and resilience (ADR-0008, ADR-0015) | [architecture/errors-and-resilience.md](architecture/errors-and-resilience.md) | ✅ |
| Background jobs (ADR-0014) | [architecture/background-jobs.md](architecture/background-jobs.md) | ✅ |
| Messaging and notifications (ADR-0007, spec 008) | [architecture/messaging.md](architecture/messaging.md) | 🟡 wiring built, notifications planned |
| Observability (ADR-0010) | [architecture/observability.md](architecture/observability.md) | ✅ traces |
| Doctor schedules (spec 006) | [architecture/doctor-schedules.md](architecture/doctor-schedules.md) | 📋 planned |
| Clinics and time zones (spec 007) | [architecture/clinics-timezones.md](architecture/clinics-timezones.md) | 📋 planned |

The requirements behind these pages are catalogued in [requirements/functional.md](requirements/functional.md) and
[requirements/non-functional.md](requirements/non-functional.md).

## Containers
```mermaid
flowchart LR
  U[Receptionist / Doctor / Admin<br/>browser] -->|HTTP :8080| W[web<br/>nginx + Angular SPA]
  W -->|/api/v1 JSON, Bearer JWT,<br/>refresh cookie| A[api<br/>ASP.NET Core 10]
  subgraph A_in[inside the api process]
    direction TB
    H[Hangfire server<br/>recurring purges]
    WV[Wolverine<br/>message store + transport]
  end
  A --- A_in
  A --> P[(PostgreSQL 17<br/>public · identity · hangfire · wolverine)]
  WV -.->|when the broker is up| R[[RabbitMQ 4]]
  A -.->|OTLP gRPC :4317| J[Jaeger]
  A -.->|planned, spec 008| M[SMTP provider]
```
- **web** is the Angular production build served by nginx, which proxies `/api/` to the API on the same origin (no CORS).
  In local development the Angular dev server on :4200 does the same proxying.
- **api** is the only host: controllers, Hangfire background jobs and Wolverine messaging all run in this one process
  ([ADR-0014](adr/0014-hangfire-background-jobs.md), [ADR-0007](adr/0007-wolverine-outbox-rabbitmq.md)).
- **PostgreSQL** holds everything durable. Its schemas are `public` (scheduling, audit, idempotency), `identity` (users,
  roles, refresh tokens), `hangfire` and `wolverine`.
- **RabbitMQ** is optional at runtime: without it Wolverine runs in degraded mode on its PostgreSQL store.

## Deployment (Docker Compose, local demo)
```mermaid
flowchart TB
  subgraph host[Developer machine]
    B[Browser]
  end
  subgraph compose[docker compose · project appointment-scheduler]
    web["web · nginx:1.27-alpine<br/>:8080 → 80"]
    api["api · aspnet:10.0<br/>:5080 → 8080<br/>ASPNETCORE_ENVIRONMENT=Development"]
    pg[("postgres:17-alpine<br/>:5432 · volume pgdata")]
    mq[["rabbitmq:4-management<br/>:5672, :15672"]]
    jg["jaeger all-in-one<br/>:16686, :4317"]
  end
  B -->|localhost:8080| web
  B -->|localhost:5080/scalar| api
  web -->|http://api:8080| api
  api -->|Host=postgres| pg
  api -->|Host=rabbitmq| mq
  api -->|http://jaeger:4317| jg
  api -. depends_on healthy .-> pg
  api -. depends_on healthy .-> mq
```
The images are built from `src/AppointmentScheduler.Api/Dockerfile` (context: the repository root) and `web/Dockerfile`.
Development mode makes this a demo stack: it migrates on start, seeds fictional data and demo users, and uses the dev-only JWT
key. Not done yet (S-61): a separate migrator, `ForwardedHeaders` behind nginx, and a production configuration.
An optional `pgadmin` service (profile `tools`, http://localhost:5050, 127.0.0.1 only) gives a pre-connected database UI
(plan 017).

## Layers
```mermaid
flowchart BT
  D[Domain<br/>entities, rules, IEntity, IRepository,<br/>repository + unit-of-work contracts, ErrorCodes] --> AP[Application<br/>services, DTOs, validators,<br/>ApplicationError — no EF Core]
  AP --> I[Infrastructure<br/>EF Core repositories on Repository&lt;T,K&gt;,<br/>unit of work, Identity, Hangfire, Wolverine]
  I --> API[Api host<br/>controllers, error envelope,<br/>idempotency, rate limiting]
  AP --> API
```
Arrows point from a project to the projects that depend on it. `ArchitectureTests` enforce the rules:
- the Api uses Application and Infrastructure only, never Domain types (`ApplicationError`, Application DTOs and enums);
- Application has no EF Core;
- repositories are reached only through `IUnitOfWork`, every contract extends `IRepository<TEntity, TKey>`, and every
  implementation inherits `Repository<TEntity, TKey>` ([ADR-0016](adr/0016-repository-unit-of-work.md)).

Identity types live in `Microsoft.AspNetCore.Identity`, so Identity stays in Infrastructure. Application owns the auth
contracts (`IAuthService`, `IUserAdminService`, `ICurrentUser`, `AuditLog`).

## Data model
```mermaid
erDiagram
  doctors ||--o{ appointments : "has"
  patients ||--o{ appointments : "has"
  identity_users ||--o{ refresh_tokens : "owns"
  identity_users }o--o{ identity_roles : "user_roles"
  identity_users }o--o| doctors : "doctor_id (Doctor role)"
  identity_users ||--o{ idempotency_records : "keys scoped per user"
  identity_users ||--o{ audit_records : "acting user (nullable)"

  doctors {
    int id PK
    string full_name
    string specialty
  }
  patients {
    int id PK
    string mrn UK
    string full_name
    date date_of_birth
    string phone
  }
  appointments {
    uuid id PK "UUID v7"
    int doctor_id FK
    int patient_id FK
    timestamptz start_utc
    timestamptz end_utc "CHECK end > start"
    string reason "1-500 chars"
    string status "Scheduled | Cancelled"
    timestamptz created_at_utc
    timestamptz cancelled_at_utc
    string cancellation_reason
    xid xmin "concurrency token"
  }
  identity_users {
    uuid id PK
    string email UK
    string password_hash
    int doctor_id FK "nullable"
    int access_failed_count
    timestamptz lockout_end
  }
  identity_roles {
    uuid id PK
    string name "Admin | Receptionist | Doctor"
  }
  refresh_tokens {
    uuid id PK
    uuid user_id FK
    bytea token_hash
    uuid family_id
    timestamptz expires_at_utc
    timestamptz revoked_at_utc
    uuid replaced_by_id
  }
  audit_records {
    uuid id PK
    uuid user_id "nullable"
    string action
    string entity_type
    string entity_id
    timestamptz timestamp_utc
    string trace_id
  }
  idempotency_records {
    uuid id PK
    uuid user_id "UNIQUE (user_id, key)"
    string key
    string method
    string path
    string request_hash "SHA-256"
    string status "InProgress | Completed"
    int response_status_code
    text response_body
    jsonb response_headers
    timestamptz expires_at_utc
  }
```
`appointments` carries two `EXCLUDE USING gist` constraints over `tstzrange(start_utc, end_utc, '[)')`, one per doctor and
one per patient, for *Scheduled* rows only ([ADR-0002](adr/0002-postgres-exclusion-constraints.md)). Cancelling is a soft
status change, so history is kept. The `hangfire` and `wolverine` schemas are owned by those libraries.

## Host composition (`Api/Program.cs`)
`Program.cs` only composes. Each concern has one `public static class <Concern>Config` in `Api/Configuration/`, written as
C# 14 extension blocks:
- `extension(WebApplicationBuilder builder) { public WebApplicationBuilder Add<Concern>Configuration() … }` for services;
- `extension(WebApplication app) { … }` for that concern's pipeline or mapping helpers.

Each layer registers its own services:

| Call | File | Registers |
|---|---|---|
| `AddApiConfiguration()` | `Api/Configuration/ApiConfig.cs` | `ClinicOptions` (validated on start), `TimeProvider`, `AddApplication()`, `AddInfrastructure()`, controllers, JSON, model-state 400, `IClientErrorFactory` + `IProblemDetailsWriter` (both → `ApiResponse`), `GlobalExceptionHandler`, `AddProblemDetails`, health checks, the global `IdempotencyFilter`; app side `UseApiExceptionHandling()` |
| ↳ `AddApplication()` | `Application/DependencyInjection.cs` | services, `AuditLog`, validators |
| ↳ `AddInfrastructure()` | `Infrastructure/DependencyInjection.cs` | `AddPersistence` (DbContext, seed, unit of work — which owns the repositories, idempotency store), `AddIdentityAndJwt`, `AddBackgroundJobs` (Hangfire) |
| `AddSecurityConfiguration()` | `Api/Configuration/SecurityConfig.cs` | role policies + JWT 401/403 bodies (`AddApiAuth`), HSTS, CORS from `Cors:AllowedOrigins` |
| `AddMessagingConfiguration()` | `Api/Configuration/MessagingConfig.cs` → `Infrastructure/Messaging/MessagingSetup.cs` | Wolverine: PostgreSQL message store (schema `wolverine`), EF Core transactions, RabbitMQ transport when the startup probe succeeds, otherwise degraded mode + `RabbitMqRecoveryService` (ADR-0007) |
| `AddApiVersioningConfiguration()` | `Api/Configuration/ApiVersioningConfig.cs` | Asp.Versioning: v1 default, URL segment + `X-Api-Version` + `api-version`, reported versions, API explorer groups (ADR-0017) |
| `AddOpenApiConfiguration()` | `Api/Configuration/OpenApiConfig.cs` | OpenAPI document (+ `Idempotency-Key` header on POSTs); app side `MapOpenApiDocs()` → `/openapi/v1.json`, `/scalar` |
| `AddObservabilityConfiguration()` | `Api/Configuration/ObservabilityConfig.cs` | OpenTelemetry traces (ASP.NET Core, HttpClient, EF Core, Wolverine) → OTLP/Jaeger; metrics → OTLP when configured (ADR-0010) |
| `AddRateLimitingConfiguration()` | `Api/Configuration/RateLimitingConfig.cs` | Polly pipelines `rate-limit:global/reads/writes/auth` (concurrency + partitioned limiters); app side `UseRateLimitingConfiguration()` after authentication (ADR-0009) |

On the app side, `ApplyMigrationsAsync()` runs first (Development only), then the middleware below in order, then
`MapControllers`, `MapHealthChecks("/health/live")`, then `UseBackgroundJobs()` (recurring jobs + Development-only
`/hangfire`). The middleware order stays written out in `Program.cs` on purpose, so it can be read in one place.

## API request pipeline
```mermaid
flowchart LR
  REQ([request]) --> SH[SecurityHeaders] --> EH[ExceptionHandler<br/>→ ApiResponse errors]
  EH --> HS[HSTS<br/>non-Development] --> CO[CORS] --> AN[Authentication<br/>JWT → 401]
  AN --> RL[Polly rate limits<br/>→ 429] --> AZ[Authorization<br/>role policies → 403]
  AZ --> IF[IdempotencyFilter<br/>POST only]
  IF --> C[Controller action] --> S[Application service<br/>validate → lookups → BR-7 →<br/>rules → unit of work commit]
```
Every framework-made error has the same `ApiResponse` shape, and its `errorCode` comes from `Api/Errors/ErrorStatusMapping`:
- bodiless statuses (404/405) go through `IProblemDetailsService` → `ApiResponseProblemDetailsWriter`;
- MVC client errors (415) go through `ApiResponseClientErrorFactory`.

Details: [errors-and-resilience.md](architecture/errors-and-resilience.md).
