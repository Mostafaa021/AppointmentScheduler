# Plans

Every approved implementation plan lives here as its own file, so it can be re-read and amended later (CLAUDE.md, Workflow).

- **Name**: `NNN-<kebab-slug>.md`, numbered in the order the plans were approved.
- **Header**: `Status: Draft | Approved | In progress | Done | Superseded by NNN`, the date, and the backlog stories, specs and
  ADRs the plan implements.
- **Changes during the work**: edit the same file (append a dated "Changes" note). Don't open a new plan for an amendment.
- **Tests in a plan** (plan 011): unit + architecture tests (and Vitest) only. Never plan integration tests, Testcontainers,
  `WebApplicationFactory`, Playwright or an in-repo k6 suite; behaviour that needs a real host or database is a live check.
- **Claude Code** writes plan-mode plans here directly (`plansDirectory` in `.claude/settings.json`). When it runs from another
  root, copy the approved plan here before coding.

| # | Plan | Status | Stories / specs |
|---|---|---|---|
| 001 | [Spec-driven foundation and the challenge (M0–M1)](001-foundation-and-challenge.md) | Done | S-01…S-15, specs 000–003 |
| 002 | [M2 security through the SDLC ceremony](002-m2-security-ceremony.md) | Done (review gates pending) | S-20…S-23, spec 004 |
| 003 | [Controllers, global exception handler, Hangfire, drop Worker + Mailpit](003-controllers-hangfire-refactor.md) | Done | S-24…S-27 |
| 004 | [ApiResponse envelope, exception vocabulary, ErrorCodes](004-api-response-envelope.md) | Done | S-28, ADR-0015 |
| 005 | [Composition root, repositories + unit of work, endpoint-only controllers](005-composition-root-and-repositories.md) | Done | S-29, S-29b, ADR-0016 |
| 006 | [Host config as extension blocks + versioning, observability, messaging](006-host-config-versioning-otel-messaging.md) | Done | S-33 (part), S-50 (wiring), S-60 (part), ADR-0007/0010/0017 |
| 007 | [ApiResponse in the house template shape](007-api-response-house-template.md) | Done | ADR-0015 |
| 008 | [RabbitMQ local credentials guest/guest](008-rabbitmq-guest-credentials.md) | Done | ADR-0007 |
| 009 | [Sprint 3 — M3 reliability](009-sprint-3-reliability.md) | Done (S-31 set aside) | S-30, S-32, S-33; spec 009, ADR-0006/0009 |
| 010 | [Repositories only through the unit of work](010-repositories-through-unit-of-work.md) | Done | ADR-0016 |
| 011 | [Unit and architecture tests only](011-unit-and-architecture-tests-only.md) | Done — rule for all later plans | — |
| 012 | [Idempotency-Key on POST only; `[SkipIdempotency]` per action](012-idempotency-post-only.md) | Done | spec 009, ADR-0006 |
