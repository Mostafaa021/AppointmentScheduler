# 015 — Judgment test (07): finish "time zones + concurrent bookings" in code and in SPEC.md

- Status: **Done** · Approved 2026-10-20 · SPEC §4–§6 · ADR-0002, ADR-0003, ADR-0004 · spec 001
- Changes: 2026-10-20 — running SPEC's race check showed deadlocks (1×201, 7×503 after up to 7 s). The user chose ordered
  advisory locks before the pre-check (`ExecuteInTransactionAsync` + `LockScheduleAsync`) plus 40P01 → 409; re-measured 1×201,
  7×409 in about 0.4 s.
- Progress: clinic today (API) ☑ · web ☑ · deadlock fix ☑ · SPEC ☑ · docs ☑ · live check ☑ (commits only when the user says so)

## Context
Task 07 asks three things:
1. Where should conflict checking live (UI, API, or the database)?
2. How are time zones and concurrent bookings of the same slot handled?
3. Explain the choices in SPEC.md.

You think the first part is done and the second isn't. Checking the code against SPEC §4–§6 confirms it:

- **§4 Conflict checking: done.** It explains the three layers and why only the database guarantees correctness. What it
  lacks is the *rejected alternatives*, and a judgment answer needs those.
- **§5 Concurrent bookings: thin.** It has the mechanism in one paragraph. Missing:
  - what each user sees;
  - retry vs. concurrency (idempotency);
  - the alternatives considered (unique index, SERIALIZABLE, `FOR UPDATE`/advisory locks, an in-process lock);
  - an honest note that ETag/If-Match (S-31) is set aside, with `xmin` still preventing lost updates;
  - a reproducible way to see it, since the integration tests that proved it were removed (plan 011).
- **§6 Time zones: one real gap in code.** SPEC says "the browser's zone never matters", but the Angular schedule pages
  start on, and jump "Today" to, the **browser's** calendar date:
  - `new Date()` in `schedule-page.ts` and `doctor-schedule-page.ts`;
  - so a receptionist whose browser is in New York at 20:00 sees *yesterday's* London clinic day.

  §6 also doesn't explain DST with a worked example, or why the alternatives were rejected.

## Changes

### 1. Clinic-local "today" (code)
- **Application** `Application/Clinic/ClinicQueries.cs` + `ClinicDto(string TimeZone, DateOnly Today, string Opens, string Closes, int SlotMinutes)`.
  - It reuses `ClinicOptions.ToDaySchedule()` / its `TimeZoneInfo`, and `TimeProvider`.
  - `Today` = the clinic-local date of `clock.GetUtcNow()`.
  - Registered in `Application/DependencyInjection.cs`.
- **Api** `Controllers/ClinicController.cs`: `GET /api/v1/clinic`, any signed-in role, `[ApiVersion(1.0)]`, route
  `api/v{version:apiVersion}/clinic`, `ProducesResponseType` 200 (401/500 come from the base class). Endpoints only.
- **Web**:
  - `core/api/scheduler-api.ts`: `clinic()` and a `Clinic` model.
  - `schedule-page.ts` and `doctor-schedule-page.ts`: the initial date and the **Today** button come from `clinic.today`, not
    `new Date()`. `toIsoDate` is unchanged, since a picked calendar date is sent as-is.
  - The page labels times with `clinic.timeZone`.
- **Tests**:
  - unit `ClinicQueriesTests`: at 23:30 UTC on 2026-07-01 (BST) the clinic's today is 2026-07-02; at 23:30 UTC on 2026-12-01
    (GMT) it is 2026-12-01; the zone is reported;
  - Vitest: the schedule page starts on the API's clinic date even when it differs from the browser's date (mock `clinic()`
    returns a fixed date);
  - existing specs get the `clinic()` stub.

### 2. SPEC.md §4–§6 rewritten as the task-07 answer
It stays short and graded: tables and bullets, detail in ADRs.
- **§4 Where conflict checking lives**: keep the three layers, and add a "Rejected alternatives" table:
  - UI only (stale, bypassable);
  - API check only (a race between check and insert);
  - a unique index on `(doctor_id, start_utc)` (misses partial overlaps and varying durations; needs a partial index for
    cancelled rows);
  - triggers (procedural, easy to get wrong under concurrency).
- **§5 Concurrent bookings of the same slot**:
  - a Mermaid timeline of two receptionists (pre-check passes twice, first INSERT wins, second gets 23P01 → 409);
  - what each client sees: the winner gets 201; the loser gets 409 `DOCTOR_CONFLICT`, and the UI closes the dialog, shows
    the message and reloads the grid;
  - the same user retrying is **not** a conflict: the `Idempotency-Key` replays the first 201;
  - concurrent *updates* (cancel vs reschedule) use the `xmin` token → 409 `CONCURRENCY_CONFLICT`, and ETag/If-Match (S-31)
    is deferred;
  - a "Rejected alternatives" table: SERIALIZABLE isolation (aborts and retries everywhere), `SELECT … FOR UPDATE` or an
    advisory lock per doctor (serializes, and every new write path must remember it), an in-process lock (breaks with more
    than one instance);
  - **"See it yourself"**: a copy-paste bash block that logs in and fires 10 parallel `POST /appointments` for one slot
    with distinct keys, then counts statuses → exactly one 201 and nine 409. This is the live check that replaces the
    removed test.
- **§6 Time zones**:
  - the rules (UTC storage, an IANA clinic zone, slots built in local time, `startUtc` + `localStart` + `timeZone` in
    responses, clinic-local `?date=`, clinic-local **today** from `GET /clinic`, `TimeProvider`);
  - a worked DST example: London on 2026-03-29, 09:00 BST = 08:00 UTC, while the day before 09:00 GMT = 09:00 UTC;
  - why gap/ambiguous handling exists although London's switches (01:00/02:00) fall outside opening hours;
  - a "Rejected alternatives" table: store local time (ambiguous at fall-back, no ordering across zones); convert in the
    browser (depends on the user's zone and the client's tzdata); fixed UTC offsets (wrong half the year);
  - multiple clinics → spec 007.
- §3 API table: add `GET /clinic`.

### 3. Docs that must agree
- `docs/requirements/functional.md`: new row FR-001-8, "the app opens on the clinic's today", ✅ with its tests; FR-007-5
  wording.
- `docs/architecture/availability.md`: the time-zone flowchart gains the clinic-today source.
- ADR-0003: a consequence line about clinic-local today.
- CHANGELOG, PROMPTS, `.claude/plans/015-judgment-test-time-zones-concurrency.md` + index.

## Verification
- `dotnet build -warnaserror`, `dotnet test` (unit + architecture: the new controller passes the convention tests);
  `cd web && npm test -- --watch=false && npm run build`.
- **Live** (local API):
  - `GET /api/v1/clinic` → today and Europe/London;
  - run SPEC's "See it yourself" block exactly as written → one 201, nine 409, and the database holds one *Scheduled* row
    for that slot; cancel it afterwards;
  - browser: the schedule opens on the clinic date and **Today** returns to it.
- Render-check the new Mermaid block, link-check SPEC.
- Stop and ask: commit, or leave it in the working tree.
