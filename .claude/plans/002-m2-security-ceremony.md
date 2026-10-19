# 002 — M2 security through the SDLC ceremony

- Status: **Done** (QA / security / code-review gates still pending) · 2026-10-19 · Backlog S-20…S-23 · Spec 004 ·
  ADR-0011…0013
- Backfilled from git history and PROMPTS P-10.

## Context
"Build M2 through the /sdlc role-agent ceremony, pausing for my approval after the architect step." The ceremony was then
paused so the build could be reviewed, and later stopped in favour of direct implementation.

## Decisions (at the approval checkpoint)
- ASP.NET Identity + JWT (15 min) with a rotating refresh-token cookie and family revocation on reuse; lockout 5 → 15 min (423).
- Roles Admin / Receptionist / Doctor; BR-7 (a doctor reads only their own schedule) is enforced in Application.
- **FluentValidation** instead of hand-written validators (user override, ADR-0013); audit table; security headers.
- The 423 lockout enumeration risk is accepted for M2; Playwright is deferred to M7.

## Delivered
Commits `ecb3cb6` (refined + designed spec 004) and `e93988b` (build).
