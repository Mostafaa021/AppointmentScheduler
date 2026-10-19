# 004 — Authentication & authorization

| Status | Refined ☑ · Designed ☑ · Built ☐ · Tested ☐ · Reviewed ☐ · Released ☐ · Documented ☐ |
|---|---|
| Milestone | M2 |
| Business rules touched | BR-7 |

## Goal
Only authenticated staff use the system, and each role can do only its job.

## Roles
| Role | Can |
|---|---|
| Admin | everything, incl. users, clinics, doctors, schedules |
| Receptionist | patients CRUD, availability, book / cancel / reschedule |
| Doctor | read own appointments only |

## Acceptance criteria
- **AC-004-1** `POST /auth/login {email,password}` with valid credentials → 200 `{accessToken, expiresAt, user{id,email,roles,doctorId?}}`
  and a refresh token in an `HttpOnly; Secure; SameSite=Strict` cookie scoped to `/api/v1/auth`.
- **AC-004-2** Invalid credentials → 401 `invalid_credentials` (same message for unknown email and wrong password).
- **AC-004-3** 5 consecutive failures lock the account for 15 min → 423 `account_locked`.
- **AC-004-4** Access token lifetime 15 min; any protected endpoint without/with expired token → 401.
- **AC-004-5** `POST /auth/refresh` with a valid refresh cookie rotates it (old one revoked, new one issued) → 200 new access token.
- **AC-004-6** Re-using an already-rotated refresh token revokes the whole token family → 401 `refresh_token_reused`.
- **AC-004-7** `POST /auth/logout` revokes the current refresh token and clears the cookie → 204.
- **AC-004-8** Role policies: Receptionist calling an Admin endpoint → 403; Doctor calling book/cancel → 403.
- **AC-004-9** (BR-7) Doctor reading another doctor's schedule → 403; own schedule → 200.
- **AC-004-10** `GET /me` returns the current user's profile and roles.
- **AC-004-11** Admin can create users and assign roles; a Doctor user must be linked to an existing Doctor.

## Data changes
ASP.NET Identity tables (`identity` schema), `AppUser.DoctorId?`, `refresh_tokens(id, user_id, token_hash, family_id, expires_at,
created_at, revoked_at, replaced_by_id)`.

## Design
See ADR-0005. JWT signed HS256 with key from configuration/env (`Jwt:SigningKey`), issuer/audience validated, clock skew 30 s.
Policies: `AdminOnly`, `CanManageAppointments` (Admin, Receptionist), `CanReadSchedule` (all roles + resource check).

## NFR notes
`auth` rate-limit policy on login/refresh; never log passwords or tokens; audit login success/failure (user id only).

## Out of scope
SSO/OIDC, MFA, password reset e-mails.
