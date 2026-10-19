# 008 — RabbitMQ local credentials guest/guest

- Status: **Done** · 2026-10-19 · ADR-0007
- Backfilled from git history and PROMPTS P-19.

## Context
The RabbitMQ dashboard rejected the login because compose created `scheduler` / `scheduler_dev_pw`.

## Decisions
- `guest` / `guest` in the compose defaults, `.env.example` and the Development appsettings; documented as local-only.
- The broker was recreated with `--renew-anon-volumes`, because the old user lived in an anonymous volume.

## Delivered
Commit `bc74cd3`.
