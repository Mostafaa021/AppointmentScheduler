# Architecture

## Containers
```mermaid
flowchart LR
  U[Receptionist / Doctor / Admin<br/>browser] --> W[web<br/>Angular + nginx]
  W -->|/api/v1 JSON, JWT| A[api<br/>ASP.NET Core]
  A --> P[(PostgreSQL<br/>appointments, identity,<br/>idempotency, outbox)]
  A -->|outbox relay| R[[RabbitMQ]]
  R --> K[worker<br/>Wolverine handlers]
  K --> P
  K -->|SMTP, Polly| M[Mailpit]
  A -. OTLP .-> J[Jaeger]
  K -. OTLP .-> J
```

## Layers
```mermaid
flowchart BT
  D[Domain<br/>entities, rules, events] --> AP[Application<br/>services, DTOs, ports]
  AP --> I[Infrastructure<br/>EF Core, Identity, Wolverine, SMTP]
  I --> API[Api host]
  I --> WK[Worker host]
```
Arrows point from a project to the projects that depend on it. Enforced by `ArchitectureTests`.

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
  API-->>R2: 409 doctor_conflict
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
  participant WK as Worker
  participant SMTP as Mailpit
  API->>DB: tx: INSERT appointment + outbox message
  DB-->>API: commit
  DB->>MQ: relay AppointmentBooked
  MQ->>WK: deliver (inbox dedupe)
  WK->>SMTP: send (timeout → retry → circuit breaker)
  WK->>MQ: schedule reminder at start − 24h
```
