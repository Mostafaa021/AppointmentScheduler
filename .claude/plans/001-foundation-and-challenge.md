# 001 — Spec-driven foundation and the challenge (M0–M1)

- Status: **Done** · Approved 2026-10-19 · Backlog S-01…S-15 · Specs 000–003 · ADR-0001…0010
- Backfilled from git history and PROMPTS P-01…P-09; the original plan text lived outside the repo.

## Context
The take-home "Appointment Scheduler": receptionists see 30-minute slots 09:00–17:00 and book, cancel and reschedule. Three
rules apply: no doctor overlap, no patient overlap across doctors, no booking in the past. The user asked for a spec-driven
plan that scales to new features, plus Claude files, skills and full documentation. Production-grade NFRs and SDLC role agents
were added during plan review.

## Decisions
- Angular 22 + .NET 10 Clean Architecture (Domain ← Application ← Infrastructure ← Api) + PostgreSQL in Docker.
- Overlaps are guarded three ways:
  - the UI hint;
  - the service pre-check (specific 409 codes);
  - **PostgreSQL exclusion constraints** (`tstzrange '[)'`, `Scheduled` rows only) as the guarantee.
- UTC storage; the clinic IANA zone is applied server-side (`DaySchedule`); `TimeProvider` everywhere.
- Milestones M1 (the exact challenge, tag `v1-challenge`) → M7 (NFRs); 10 SDLC role agents + `/sdlc`, `/new-feature`,
  `/add-migration`, `/load-test`, `/log-prompt`, `/verify` skills.

## Delivered
Commits `d1a88e2`, `a903a19`, `2e30015` (M0), `3eb8bf8`, `0d014cc` (M1, tag `v1-challenge`).
