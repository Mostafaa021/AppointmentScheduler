# Architecture

## Containers
```mermaid
flowchart LR
  U[Receptionist / Doctor / Admin<br/>browser] --> W[web<br/>Angular + nginx]
  W -->|/api/v1 JSON, JWT| A[api<br/>ASP.NET Core]
  A --> P[(PostgreSQL<br/>appointments, identity,<br/>idempotency, outbox)]
  A -->|outbox relay| R[[RabbitMQ]]
  R -->|Wolverine handlers, in-process| A
  K --> P
  A -->|Hangfire jobs: e-mail via SMTP + Polly, reminders, cleanup| M[SMTP provider<br/>logging sender in Development]
  A -. OTLP .-> J[Jaeger]
  K -. OTLP .-> J
```

## Layers
```mermaid
flowchart BT
  D[Domain<br/>entities, rules, events,<br/>repository + unit-of-work contracts] --> AP[Application<br/>services, DTOs, validators<br/>no EF Core]
  AP --> I[Infrastructure<br/>EF Core repositories, unit of work,<br/>Identity, Hangfire, Wolverine, SMTP]
  I --> API[Api host]
```
Arrows point from a project to the projects that depend on it. Enforced by `ArchitectureTests`. The Api uses Application and
Infrastructure only, never Domain types (`ApplicationError`, Application DTOs/enums).
Identity types live in `Microsoft.AspNetCore.Identity`, so Identity stays in Infrastructure; Application owns the auth
contracts (`IAuthService`, `IUserAdminService`, `ICurrentUser`, `AuditLog`) — spec 004.

## Host composition (`Api/Program.cs`)
`Program.cs` only composes. Each concern has one `public static class <Concern>Config` in `Api/Configuration/`, written as
C# 14 extension blocks:
- `extension(WebApplicationBuilder builder) { public WebApplicationBuilder Add<Concern>Configuration() … }` for services;
- `extension(WebApplication app) { … }` for that concern's pipeline or mapping helpers.

Each layer registers its own services:

| Call | File | Registers |
|---|---|---|
| `AddApiConfiguration()` | `Api/Configuration/ApiConfig.cs` | `ClinicOptions` (validated on start), `TimeProvider`, `AddApplication()`, `AddInfrastructure()`, controllers, JSON, model-state 400, `IClientErrorFactory` + `IProblemDetailsWriter` (both → `ApiResponse`), `GlobalExceptionHandler`, `AddProblemDetails`, health checks; app side `UseApiExceptionHandling()` |
| ↳ `AddApplication()` | `Application/DependencyInjection.cs` | services, `AuditLog`, validators |
| ↳ `AddInfrastructure()` | `Infrastructure/DependencyInjection.cs` | `AddPersistence` (DbContext, seed, unit of work — which owns the repositories), `AddIdentityAndJwt`, `AddBackgroundJobs` (Hangfire) |
| `AddSecurityConfiguration()` | `Api/Configuration/SecurityConfig.cs` | role policies + JWT 401/403 bodies (`AddApiAuth`), HSTS, CORS from `Cors:AllowedOrigins` |
| `AddMessagingConfiguration()` | `Api/Configuration/MessagingConfig.cs` → `Infrastructure/Messaging/MessagingSetup.cs` | Wolverine: PostgreSQL message store (schema `wolverine`), EF Core transactions, RabbitMQ transport when the startup probe succeeds, otherwise degraded mode + `RabbitMqRecoveryService` (ADR-0007) |
| `AddApiVersioningConfiguration()` | `Api/Configuration/ApiVersioningConfig.cs` | Asp.Versioning: v1 default, URL segment + `X-Api-Version` + `api-version`, reported versions, API explorer groups (ADR-0017) |
| `AddOpenApiConfiguration()` | `Api/Configuration/OpenApiConfig.cs` | OpenAPI document; app side `MapOpenApiDocs()` → `/openapi/v1.json`, `/scalar` |
| `AddObservabilityConfiguration()` | `Api/Configuration/ObservabilityConfig.cs` | OpenTelemetry traces (ASP.NET Core, HttpClient, EF Core, Wolverine) → OTLP/Jaeger; metrics → OTLP when configured (ADR-0010) |
| `AddRateLimitingConfiguration()` | `Api/Configuration/RateLimitingConfig.cs` | Polly pipelines `rate-limit:global/reads/writes/auth` (concurrency + partitioned limiters); app side `UseRateLimitingConfiguration()` after authentication (ADR-0009) |

Every framework-made error has the same `ApiResponse` shape. The `errorCode` comes from `Api/Errors/ErrorStatusMapping`:
- bodiless statuses (404/405) go through `UseStatusCodePages` → `IProblemDetailsService` → `ApiResponseProblemDetailsWriter`;
- MVC client errors (415) go through `ApiResponseClientErrorFactory`.

On the app side: `ApplyMigrationsAsync()` (Development only), then the middleware below in order, then `MapControllers`,
`MapHealthChecks("/health/live")`, then `UseBackgroundJobs()` (recurring jobs + Development-only `/hangfire`).
The middleware order stays written out in `Program.cs` on purpose, so you can read it in one place.

## API request pipeline (M2)
`SecurityHeaders` → `ExceptionHandler` (`SchedulerException` → `ApiResponse` error with `errorCode`, ADR-0015) → `StatusCodePages` (bodiless 404/405 → `ApiResponse`) → `Hsts` (non-Development)
→ `Cors` → `Authentication` (JWT, 401 `UNAUTHORIZED`) → `PollyRateLimiting` (429 `RATE_LIMITED`) → `Authorization` (role policies, 403 `FORBIDDEN`) → endpoint
(bind → Application service: validate → lookups → BR-7 check → rules → repositories reached through `IUnitOfWork`, one `SaveChangesAsync` incl. audit row).

## Sign-in, refresh rotation and reuse detection (spec 004, ADR-0005)
```mermaid
sequenceDiagram
  participant B as Browser (Angular, token in memory)
  participant API
  participant DB as PostgreSQL (identity.*, audit_records)
  B->>API: POST /auth/login {email, password}
  API->>DB: reserve attempt (atomic UPDATE, not locked, count < 5)
  API->>DB: verify hash → reset count, INSERT refresh token (new family), audit login_succeeded
  API-->>B: 200 {accessToken 15 min, user} + Set-Cookie refresh_token (HttpOnly, Secure, Strict, Path=/api/v1/auth)
  B->>API: GET /appointments… Authorization: Bearer
  API-->>B: 401 UNAUTHORIZED (token expired)
  B->>API: POST /auth/refresh (cookie) — one call for all pending 401s
  API->>DB: claim token (UPDATE … WHERE not revoked/replaced) + INSERT successor (same family)
  API-->>B: 200 new accessToken + new cookie
  B->>API: retry original request once
  Note over B,API: Later, the old (rotated) cookie is presented again, e.g. by a thief
  API->>DB: token already replaced → revoke whole family, audit REFRESH_TOKEN_REUSED
  API-->>B: 401 REFRESH_TOKEN_REUSED to the caller (newest token now → 401 INVALID_REFRESH_TOKEN)
```

## Booking a slot (happy path + race)
```mermaid
sequenceDiagram
  participant R1 as Receptionist A
  participant R2 as Receptionist B
  participant API
  participant DB as PostgreSQL
  R1->>API: POST /appointments (09:30)
  R2->>API: POST /appointments (09:30)
  API->>DB: pre-check overlaps (both see none)
  API->>DB: INSERT A
  DB-->>API: ok
  API-->>R1: 201 Created
  API->>DB: INSERT B
  DB-->>API: 23P01 exclusion violation
  API-->>R2: 409 DOCTOR_CONFLICT
```

## Idempotent retry
```mermaid
sequenceDiagram
  participant C as Client
  participant F as IdempotencyFilter
  participant H as Handler
  participant DB
  C->>F: POST + Idempotency-Key K
  F->>DB: INSERT (user,K) InProgress
  F->>H: run
  H-->>F: 201 body
  F->>DB: UPDATE Completed + response
  F-->>C: 201 (response lost in network)
  C->>F: retry POST + K
  F->>DB: SELECT (user,K) → Completed, same hash
  F-->>C: 201 same body, Idempotent-Replayed: true
```

## Notification flow
```mermaid
sequenceDiagram
  participant API
  participant DB as PostgreSQL (outbox)
  participant MQ as RabbitMQ
  participant WK as Api background (Wolverine handler / Hangfire job)
  participant SMTP as SMTP (logging sender in dev)
  API->>DB: tx: INSERT appointment + outbox message
  DB-->>API: commit
  DB->>MQ: relay AppointmentBooked
  MQ->>WK: deliver (inbox dedupe)
  WK->>SMTP: send (timeout → retry → circuit breaker)
  WK->>MQ: schedule reminder at start − 24h
```
