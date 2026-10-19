# 003 — Controllers, global exception handler, Hangfire, drop Worker + Mailpit

- Status: **Done** · 2026-10-19 · Backlog S-24…S-27 · ADR-0014
- Backfilled from git history and PROMPTS P-11, P-12.

## Context
"Use controllers instead of Minimal APIs, a global exception handler; why a separate Worker dll; what is Mailpit for?" The
answers chosen: Hangfire inside the app instead of the worker, remove Mailpit, keep RabbitMQ/Wolverine, and set Hangfire up now
with one real job.

## Decisions
- `ApiControllerBase` (authenticated by default), one controller per feature, same routes and codes.
- `GlobalExceptionHandler`: domain exceptions map to 4xx; anything else → logged generic 500.
- Hangfire in the Api (PostgreSQL storage, Development-only dashboard), with an hourly expired refresh-token purge.
  `Newtonsoft.Json` is pinned centrally (NU1903).

## Delivered
Commits `8e620ef`, `f07b999`, `3ac7d0e`, `2911ef8`, `c0543ff`.
