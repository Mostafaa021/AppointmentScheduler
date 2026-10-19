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
- Authorization via policies on endpoint groups plus a resource check for "Doctor reads own schedule".

## Alternatives
Keycloak/OIDC (more realistic, more moving parts — rejected for scope); cookie-only auth (simpler but less representative of API
clients/k6).

## Consequences
+ Self-contained, testable with WebApplicationFactory, k6 can log in via API.
− We own token security code → covered by tests and the security-engineer review.
