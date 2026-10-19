# ADR-0005 — Authentication: ASP.NET Core Identity + JWT access tokens + rotating refresh tokens

- Status: Accepted · Date: 2026-10-19

## Context
Need staff login with roles (Admin, Receptionist, Doctor) for an Angular SPA, simulating production without an external IdP.

## Decision
- ASP.NET Core Identity (EF stores in Postgres, schema `identity`) for users, password hashing, lockout (5 tries / 15 min), roles.
- Login issues a **15-minute JWT** (HS256, key from env `JWT_SIGNING_KEY` ≥ 32 bytes, issuer/audience validated) returned in the
  body and kept **in memory** by the SPA (not localStorage → reduces XSS token theft).
- A **7-day refresh token** (random 256-bit, stored as SHA-256 hash) is set as an `HttpOnly; Secure; SameSite=Strict` cookie with
  path `/api/v1/auth`. `/auth/refresh` rotates it; reuse of a rotated token revokes the whole family (theft detection).
- Authorization via role policies on controllers/actions plus a resource check for "Doctor reads own schedule".

## Alternatives
Keycloak/OIDC (more realistic, more moving parts — rejected for scope); cookie-only auth (simpler but less representative of API
clients/k6).

## Consequences
+ Self-contained; any HTTP client (curl, k6) can log in via the API.
− We own token security code → covered by tests and the security-engineer review.

## Amendments
- **2026-10-19 — spec 004 design** (approved by human 2026-10-19). The decision above stands; these refine *how*:
  - **Lockout timing uses `TimeProvider`, not Identity's lockout methods.** `UserManager.AccessFailedAsync` /
    `IsLockedOutAsync` read the system clock (`Microsoft.Extensions.Identity.Core` 10.0 has no `TimeProvider` support), which
    breaks the project rule and makes AC-004-3 untestable with `FakeTimeProvider`. Identity's columns
    (`access_failed_count`, `lockout_end`) are still used; the auth service updates them with atomic conditional `UPDATE`s
    (also closes the parallel-guess race). Values unchanged: 5 failures → 15 min.
  - **Access-token lifetime is validated with a `TimeProvider`-based `LifetimeValidator`** (skew 30 s), `ValidAlgorithms` =
    HS256 only, `MapInboundClaims = false` (`sub`, `role`, `doctor_id` claims).
  - **Keys:** Identity uses `Guid` keys (`IdentityUser<Guid>`, `IdentityRole<Guid>`); the three roles are seeded by migration.
  - **Password policy** fixed: 12–128 characters with at least one lowercase letter, one uppercase letter and one digit;
    no symbol requirement.
  - Refresh-token rotation claims the presented token with a conditional `UPDATE` (0 rows → treated as reuse), so two
    concurrent refreshes cannot both succeed.
  - Related: [ADR-0011](0011-audit-log-table.md) (audit), [ADR-0012](0012-br7-resource-check-in-application.md) (BR-7),
    [ADR-0013](0013-request-validation.md) (validation).
