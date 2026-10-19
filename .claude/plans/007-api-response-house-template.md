# 007 — ApiResponse in the house template shape

- Status: **Done** · 2026-10-19 · ADR-0015
- Backfilled from git history and PROMPTS P-18.

## Context
"Make the API response like this template": `IsSuccess`, `StatusCode`, `Message`, `Data`, `ErrorDetails`, `Timestamp`, and the
factories `Success` / `Error` / `NotFound` / `BadRequest` / `Created` / `NoContent`.

## Decisions (user choices)
- Template shape plus the existing `errorCode`, `errors` and `traceId`, which the Angular app relies on.
- `errorDetails` only for unexpected 500s, in Development only; a Staging test proves nothing leaks.

## Delivered
Commit `cbdeb5a`.
