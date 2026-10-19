# ADR-0012 — BR-7 (Doctor reads only own appointments) is enforced in the Application service via `ICurrentUser`

- Status: Accepted (human approval 2026-10-19) · Date: 2026-10-19 · Spec: [004](../specs/004-auth.md) (AC-004-8, AC-004-9), [003](../specs/003-manage-schedule.md) (AC-003-9)

## Context
Role policies on controllers (ADR-0005) decide *which routes* a role may call. BR-7 is narrower: a user whose only role
is Doctor may read `GET /appointments?doctorId=D` and `GET /appointments/{id}` only for their own doctor D. For the
by-id route the doctor is known only after loading the appointment, and AC-004-9 requires 404 for an unknown id but 403 for
another doctor's appointment.

## Options
1. **Application service check** — `AppointmentService` calls `currentUser.EnsureCanRead(doctorId)` (port `ICurrentUser`,
   implemented in Api from the JWT claims) and throws `ForbiddenException` → 403 `forbidden`.
2. **ASP.NET Core resource-based authorization** — `IAuthorizationHandler<SameDoctorRequirement, int>`, invoked from the
   endpoint with `IAuthorizationService.AuthorizeAsync(user, doctorId, …)`.
3. **Endpoint filter** that reads `doctorId` from the query / loads the appointment before the handler.
4. **EF global query filter** on `Appointment.DoctorId` for doctor-only users.

## Decision
Option 1.
- The by-id case needs the loaded appointment; the service already loads it, so the check costs no extra query and the
  404-before-403 order falls out naturally.
- The rule is business logic (BR-x) and stays next to the other rules, unit-testable without HTTP.
- Option 2 forces the endpoint to load data or call the service twice — endpoints must stay thin (ADR-0001).
- Option 3 duplicates the lookup outside the service. Option 4 turns 403 into 404/empty results, contradicting AC-004-9,
  and silently affects every query.

Route-level role checks stay in Api policies, so AC-004-8's precedence (401 → 403 by role → validation/lookups → BR-7)
holds: only `ScheduleReader` routes reach the service check.

## Consequences
+ One small static helper (`ScheduleAccess.EnsureCanRead`), covered by unit and integration tests.
+ Application stays free of `Microsoft.AspNetCore` (ArchitectureTests rule unchanged).
− Every new Doctor-readable query must remember to call the check; the integration test sweep for AC-004-9 and code review
  are the guard.
− Role/doctor data comes from the access token, so changes apply at the next refresh (accepted by AC-004-14).
