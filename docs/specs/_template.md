# NNN — <Feature name>

| Status | Refined ☐ · Designed ☐ · Built ☐ · Tested ☐ · Reviewed ☐ · Released ☐ · Documented ☐ |
|---|---|
| Milestone | M? |
| Business rules touched | BR-? |

## Goal
One or two sentences: the outcome, for whom.

## User story
As a <role>, I want <capability>, so that <benefit>.

## Acceptance criteria
- **AC-NNN-1** Given … When … Then …
- **AC-NNN-2** …

## API contract
Bodies are `ApiResponse<T>` (ADR-0015). List every status per route — each becomes a `[ProducesResponseType]` — with the
`errorCode`s (UPPER_SNAKE; new ones are added to `Domain/Common/ErrorCodes.cs`).

| Method | Route | Request | Responses (status → `data` type or `errorCode`s) |
|---|---|---|---|

New error codes:
| Code | Status | Exception | Meaning |
|---|---|---|---|

## Data changes
Entities / columns / constraints / migrations.

## Design (architect)
Layers touched, new types, decisions (link ADRs).

## NFR notes
Auth/roles · idempotency · rate-limit policy · observability (spans/metrics) · time zones · PII.

## UI notes
Screens, states (loading / empty / error), accessibility.

## Test plan (qa)
| AC | Test (project › class › method) |
|---|---|

## Out of scope
