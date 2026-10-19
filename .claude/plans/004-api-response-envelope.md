# 004 — ApiResponse envelope, exception vocabulary, ErrorCodes

- Status: **Done** · 2026-10-19 · Backlog S-28 · ADR-0015
- Backfilled from git history and PROMPTS P-13.

## Context
The user wanted the house style: every endpoint returns `ApiResponse<T>` with `[ProducesResponseType]` per status, exceptions
carry their own status and a log-only `LogDetail`, and codes come from one `ErrorCodes` class.

## Decisions
- Business rules stay 422; codes are UPPER_SNAKE; REST routes are kept (no action-name routes).
- `SchedulerException` hierarchy (400/401/403/404/409/422/423); every framework-made error uses the same envelope.
- Angular unwraps `data` in an interceptor and branches on `errorCode`.

## Delivered
Commits `2353f37`, `2b70dc7`.
