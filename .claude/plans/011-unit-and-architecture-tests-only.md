# 011 — Unit and architecture tests only

- Status: **Done** · 2026-10-20 · user decision · applies to every later plan
- Progress: test project removed ☑ · packages/CI ☑ · Claude files ☑ · docs ☑ (commits only when the user says so)

## Context
The user asked to remove Testcontainers and the integration tests, and to keep only unit tests and architecture tests.
Every future plan must follow this rule. Playwright e2e and the in-repo k6 suite were planned but never built, and they are
dropped too. Load testing happens outside the repo, with scripts and k6 Studio.

## Rule for all coming plans
- Tests are xUnit **unit tests** (fakes, `FakeTimeProvider`, a fake `IUnitOfWork`), NetArchTest **architecture tests**, and
  **Vitest** specs for Angular.
- Never add or plan integration tests, Testcontainers, `WebApplicationFactory`, a test database, Playwright, or an in-repo k6
  suite.
- Behaviour only a real host or database shows is a **live check** recorded in the spec (`curl` against the running API).
  Examples: exclusion constraints, idempotency replay, rate limits, a 503 on a database outage.

## Changes
- **Deleted** `tests/AppointmentScheduler.IntegrationTests` (123 tests), together with:
  - its `.slnx` entry;
  - the `Microsoft.AspNetCore.Mvc.Testing` and `Testcontainers.PostgreSql` package versions;
  - `public partial class Program`.
- **Kept** the two pure tests in `RateLimitPartitionKeyTests` by moving them to `UnitTests/RateLimiting/`. UnitTests now
  references the Api project, for pure Api helpers only.
- **Claude files:**
  - CLAUDE.md: stack, commands and workflow step 2;
  - agents: backend-developer, qa-engineer, architect, code-reviewer, frontend-developer, devops-engineer;
  - skills: `/verify`, `/new-feature`, `/add-migration`; the `/load-test` skill is deleted;
  - `settings.json` no longer allows `npx playwright`.
- **Docs:**
  - README, SPEC, sdlc.md, NFR table;
  - spec test plans 001–004 and 009;
  - ADR-0002/0005/0011/0012/0014 wording;
  - backlog S-70 / S-71;
  - `web/README.md` e2e section;
  - the CI comment.
  - Past CHANGELOG / PROMPTS entries and older plans stay as history.

## Consequences
- `dotnet test` needs no Docker and runs in seconds.
- Nothing automated proves the exclusion constraints, migrations, middleware order, idempotency storage, rate limits or
  auth cookies end to end anymore. Those are covered by live checks and code review only.
