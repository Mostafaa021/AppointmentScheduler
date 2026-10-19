# ADR-0013 — Request validation: FluentValidation validators in Application, mapped to the domain `ValidationException`

- Status: Accepted (human decision 2026-10-19; rev. 2 — rev. 1 proposed hand-written validators and was rejected) ·
  Spec: [004](../specs/004-auth.md) (AC-004-12, AC-004-22)

## Context
AC-004-22 needs field validation on 6 request types (login, book, cancel, reschedule, create user, set roles) that runs
after authentication/authorization and before lookups (401 → 403 → 400 → 404/409/422), reports all failing fields at once
and keeps today's 400 shape (`title: "Validation failed"`, `code: "validation_failed"`, camelCase `errors`). Today the only
validation is `Appointment.NormalizeReason` throwing the domain `ValidationException`, which `GlobalExceptionHandler` (formerly `DomainExceptionHandler`)
already maps to exactly that shape. Backlog S-23 names FluentValidation.

## Options
1. **FluentValidation** — one `AbstractValidator<T>` per request record, in the Application feature folder.
2. **Hand-written validators** — a static `Validate(request)` per type plus a small error collector (rev. 1 proposal).
3. **.NET 10 built-in Minimal API validation** (`AddValidation()`, DataAnnotations / `IValidatableObject`).

## Decision
Option 1, package **`FluentValidation`** (12.x) in Application only.
- **No `FluentValidation.DependencyInjectionExtensions`:** the six validators are registered explicitly in
  `Application/DependencyInjection.cs` (`AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>()` …). Six lines do
  not justify a package; Development's `ValidateOnBuild` fails startup if a service asks for an unregistered validator.
- **Validation runs inside the Application service**, as the first statement of each use-case method:
  `validator.ValidateOrThrow(request)` (extension in `Application/Common/ValidationExtensions.cs`). Not an Api endpoint
  filter, because (a) every caller of the service (controllers now, background jobs/tests later) gets validation, (b) the
  "validate before lookups" order sits in one readable method, and (c) endpoints stay bind → service → map (ADR-0001).
  Precedence still holds: authentication and authorization middleware run before the endpoint is invoked, so 401/403 come
  first by construction.
- **Mapping:** `ValidateOrThrow` runs the validator synchronously (rules are pure — no DB access; existence checks stay
  404/409 lookups) and, on failure, throws the existing **domain `ValidationException`** with failures grouped by
  property name. Names are converted to the response's camelCase with `JsonNamingPolicy.CamelCase` and cut at the first
  `[` or `.` (so `Roles[1]` → `roles`). The exception handler is unchanged; the 400 shape is unchanged.
- FluentValidation's own `ValidationException` is never thrown (no `ValidateAndThrow`).
- **Domain invariants stay** (e.g. `Appointment` reason 1–500 after trim) as the second guard for any path that bypasses
  a validator.

Rejected: option 2 (fewer dependencies, but the human chose FluentValidation as the project convention, matching backlog
S-23 and ADR-0001's "validators"); option 3 (its own ProblemDetails without `code`, validation would live in Api, and
cross-field rules such as role Doctor ⇔ `doctorId` are awkward as attributes).

## Consequences
+ Declarative, conventional rules; each validator is unit-testable (`UnitTests` references Application).
+ Response shape and handler untouched.
− One new package in Application; the ArchitectureTests Domain rule gains `FluentValidation` in its forbidden list so the
  library never leaks into Domain.
− Validators must be registered by hand when a request type is added.
