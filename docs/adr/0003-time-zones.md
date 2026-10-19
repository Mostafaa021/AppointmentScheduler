# ADR-0003 — Time zones: store UTC, schedule in clinic wall-clock time

- Status: Accepted · Date: 2026-10-19

## Context
"09:00–17:00" is clinic wall-clock time; receptionists' browsers may be in another zone; DST shifts UTC offsets twice a year.

## Decision
- Persist instants only as `timestamptz` (UTC) — `StartUtc`, `EndUtc`.
- Each clinic has an IANA zone (`Europe/London` in seed — has DST, so tests are meaningful). M1 reads it from `Clinic:TimeZone`
  config; M4 moves it onto the `clinics` table.
- Working hours are stored as local `time` values. Slots are generated in local time for the requested local date and converted
  to UTC with `TimeZoneInfo`. Invalid local times (DST gap) are skipped; ambiguous ones use the standard offset.
- The API returns `startUtc` **and** `localStart` + `timeZone`. The UI displays `localStart` and posts back `startUtc`, so the
  browser zone never matters.
- "Now" is `TimeProvider.GetUtcNow()` (fakeable in tests).

## Consequences
+ DST-safe, multi-clinic ready, unambiguous API.
− Day boundaries must be computed per clinic zone (helper on `ClinicTime`), not with `DateTime.Date`.
