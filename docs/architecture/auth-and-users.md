# Authentication, users and audit ✅

Sign-in, short-lived tokens with rotating refresh, roles, the Doctor's own-schedule rule, user admin and the audit trail.
[Spec 004](../specs/004-auth.md) · requirements FR-004-x in
[functional.md](../requirements/functional.md#004--authentication-users-and-audit--spec--architecture) ·
[ADR-0005](../adr/0005-identity-jwt.md), [ADR-0011 audit](../adr/0011-audit-log-table.md),
[ADR-0012 BR-7](../adr/0012-br7-resource-check-in-application.md).

## Components
```mermaid
flowchart LR
  subgraph Web[Angular]
    LP[login-page.ts] --> AS[auth.service.ts<br/>token in memory only]
    AI[auth.interceptor.ts<br/>Bearer + one refresh for all 401s]
  end
  subgraph Api
    AC[AuthController<br/>login · refresh · logout · me<br/>SkipIdempotency, rate policy auth]
    UC[UsersController<br/>Admin only]
    RC[Auth/RefreshTokenCookie]
    POL[Auth/Policies + ApiControllerBase<br/>authenticated by default]
  end
  subgraph Application
    IAS[IAuthService / IUserAdminService]
    SA[ScheduleAccess — BR-7]
    AL[AuditLog]
  end
  subgraph Infrastructure
    IA[IdentityAuthService]
    IU[IdentityUserAdminService]
    TI[TokenIssuer — HS256 JWT]
    PJ[PurgeExpiredRefreshTokensJob — hourly]
  end
  DB[(identity.users, roles,<br/>refresh_tokens, audit_records)]
  LP --> AC
  AC --> IAS --> IA --> DB
  AC --> RC
  UC --> IU --> DB
  IA --> TI
  IA --> AL
  IU --> AL
  PJ --> DB
```

## Sign-in, refresh rotation and reuse detection
```mermaid
sequenceDiagram
  participant B as Browser (Angular, token in memory)
  participant API
  participant DB as PostgreSQL (identity.*, audit_records)
  B->>API: POST /auth/login {email, password}
  API->>DB: reserve attempt (atomic UPDATE: not locked, count < 5)
  API->>DB: verify hash → reset count, INSERT refresh token (new family), audit login_succeeded
  API-->>B: 200 {accessToken 15 min, user} + Set-Cookie refresh_token (HttpOnly, Secure, Strict, Path=/api/v1/auth)
  B->>API: GET /appointments… Authorization: Bearer
  API-->>B: 401 UNAUTHORIZED (token expired)
  B->>API: POST /auth/refresh (cookie) — one call for all pending 401s
  API->>DB: claim token (UPDATE … WHERE not revoked/replaced) + INSERT successor (same family)
  API-->>B: 200 new accessToken + new cookie
  B->>API: retry the original request once
  Note over B,API: Later, the old (rotated) cookie is presented again, e.g. by a thief
  API->>DB: token already replaced → revoke the whole family, audit refresh_token_reused
  API-->>B: 401 REFRESH_TOKEN_REUSED (the newest token now → 401 INVALID_REFRESH_TOKEN)
```

## Account lockout
```mermaid
stateDiagram-v2
  [*] --> Active
  Active --> Active: login ok → failed count reset to 0
  Active --> Active: wrong password, failed count 1–4<br/>401 INVALID_CREDENTIALS
  Active --> Locked: 5th consecutive failure<br/>423 ACCOUNT_LOCKED
  Locked --> Locked: any login<br/>423 ACCOUNT_LOCKED
  Locked --> Active: 15 minutes pass
```
The attempt is reserved with one atomic `UPDATE`, so parallel guesses can't slip past the counter. The login endpoint also
has its own rate limit: 5 per minute per IP ([rate-limiting.md](rate-limiting.md)).

## Authorization: roles, then BR-7
```mermaid
flowchart TD
  Q[request with Bearer token] --> A{valid JWT?}
  A -- no --> U[401 UNAUTHORIZED]
  A -- yes --> P{role policy for this endpoint}
  P -- denied --> F[403 FORBIDDEN]
  P -- allowed --> D{reads a doctor's schedule<br/>or appointment?}
  D -- no --> OK[handler]
  D -- yes --> S{"ScheduleAccess.EnsureCanRead:<br/>Receptionist/Admin → any doctor<br/>Doctor-only → own doctorId"}
  S -- allowed --> OK
  S -- other doctor / no link --> F
```
| Endpoint group | Receptionist | Doctor | Admin |
|---|---|---|---|
| Availability, day schedule, get appointment | ✅ | own doctor only | ✅ |
| Book, cancel, reschedule | ✅ | 403 | ✅ |
| Doctors list, patient search | ✅ | ✅ | ✅ |
| `POST /users`, `PUT /users/{id}/roles` | 403 | 403 | ✅ |

## User admin
```mermaid
sequenceDiagram
  participant Adm as Admin
  participant API as UsersController
  participant U as IdentityUserAdminService
  participant DB as PostgreSQL
  Adm->>API: POST /users {email, password, roles, doctorId?} + Idempotency-Key
  API->>U: validate (e-mail, password policy, roles, Doctor ⇔ doctorId) → 400 with every failing field
  U->>DB: tx: doctor exists? (404), INSERT user + roles, audit user.created
  alt e-mail taken
    DB-->>Adm: 409 EMAIL_TAKEN
  end
  API-->>Adm: 201 UserDto
  Adm->>API: PUT /users/{id}/roles {roles, doctorId?} — no key needed (idempotent by design)
  U->>DB: tx: replace roles + doctor link, audit user.roles_changed
  API-->>Adm: 200 — applies at the user's next login or refresh
```

## Audit trail
Every row is written by `AuditLog.Record` into the same unit of work as the change, so a rejected change leaves no record.
Rows hold ids only, never names or e-mails (NFR-9).

| Action | When |
|---|---|
| `auth.login_succeeded` / `auth.login_failed` / `auth.logout` / `auth.refresh_token_reused` | sign-in events |
| `appointment.booked` / `appointment.cancelled` / `appointment.rescheduled` | schedule changes |
| `user.created` / `user.roles_changed` | user admin |
