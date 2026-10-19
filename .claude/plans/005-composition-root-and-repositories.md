# 005 — Composition root, repositories + unit of work, endpoint-only controllers

- Status: **Done** · 2026-10-19 · Backlog S-29, S-29b · ADR-0016
- Backfilled from git history and PROMPTS P-14…P-16.

## Context
Three requests:
- "Follow this Program.cs template; use extension methods instead of a lot of service injection";
- "use repository and unit of work";
- "controllers hold only endpoints".

## Decisions
- `Program.cs` only chains `Add…Configuration()` and orders middleware; `AddInfrastructure` is split into named parts.
- Repository per aggregate (contracts in Domain, `internal sealed` EF implementations), one `IUnitOfWork.SaveChangesAsync` per
  use case; Application dropped EF Core (architecture tests enforce it); first service unit tests with fakes.
- Cookie handling moved out of `AuthController` into `Api/Auth/RefreshTokenCookie`; architecture tests forbid helpers and
  constants in controllers.

## Delivered
Commits `0e6f782`, `ed31eb3`, `c9db7ac`, `7b8f57d`, `456b937`.
