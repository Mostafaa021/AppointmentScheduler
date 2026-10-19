# ADR-0002 — Conflict checking: UI hint, API pre-check, DB exclusion constraint as the source of truth

- Status: Accepted · Date: 2026-10-19

## Context
BR-1/BR-2: no overlapping Scheduled appointments per doctor or per patient. Must hold with two receptionists clicking the same
slot at the same moment, and with variable durations later (M4).

## Options
1. **UI only** — trivially bypassed, stale data; never sufficient.
2. **API check only** (`SELECT … overlaps` then `INSERT`) — race window between check and insert under READ COMMITTED.
3. **Unique index on (doctor_id, start_utc)** — blocks identical starts only; misses 09:00–10:00 vs 09:30–10:00 overlaps.
4. **SERIALIZABLE transactions** — correct but retry-heavy and easy to forget on a new code path.
5. **PostgreSQL exclusion constraint on time ranges** — the DB rejects any overlapping row atomically.

## Decision
Use all three layers, each with a different job:
- **UI** only offers Free slots (UX).
- **Application** pre-checks overlaps to return a specific, friendly `409 DOCTOR_CONFLICT` / `PATIENT_CONFLICT`.
- **Database** guarantees it:
  ```sql
  CREATE EXTENSION IF NOT EXISTS btree_gist;
  ALTER TABLE appointments ADD CONSTRAINT ex_appointments_doctor_overlap
    EXCLUDE USING gist (doctor_id WITH =, tstzrange(start_utc, end_utc, '[)') WITH &&) WHERE (status = 'Scheduled');
  ALTER TABLE appointments ADD CONSTRAINT ex_appointments_patient_overlap
    EXCLUDE USING gist (patient_id WITH =, tstzrange(start_utc, end_utc, '[)') WITH &&) WHERE (status = 'Scheduled');
  ```
  Half-open ranges `[)` let 09:00–09:30 and 09:30–10:00 coexist. Cancelled rows are excluded, so cancelling frees the slot.
  Violations (SQLSTATE `23P01`) are translated in Infrastructure to `ConflictException` → 409.

## Consequences
+ Correct under any concurrency and any duration; every future write path is protected automatically.
− Ties us to PostgreSQL (accepted; chosen DB). The constraint has no automated test since integration tests were dropped
  (plan 011): the Domain pre-checks are unit-tested, and the constraint itself is checked live (two parallel bookings of one
  slot → exactly one 201, the other 409).
