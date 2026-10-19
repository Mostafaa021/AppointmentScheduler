# ADR-0015 — `ApiResponse<T>` envelope, `SchedulerException` vocabulary and `ErrorCodes` constants

- Status: Accepted · Date: 2026-10-19 · Supersedes the RFC 7807 ProblemDetails error shape (SPEC.md §3, spec 004, ADR-0013's
  "400 shape")

## Context
Errors were RFC 7807 ProblemDetails (`code` extension) while successes were bare DTOs, so clients parsed two shapes. Error codes
were string literals spread over Domain, Application and Infrastructure; the exception → status mapping lived in a switch in
the Api. The team's house style (used in its other services) is one generic envelope, exceptions that know their own status,
and one constants class for codes.

## Decision
- **One envelope for every body**: `Api/Contracts/ApiResponse<T>`, following the house template.
  - Template members: `isSuccess`, `statusCode`, `message`, `data?`, `errorDetails?`, `timestamp`.
  - Template factories: `Success`, `Error`, `NotFound`, `BadRequest`, `Created`, `NoContent`, with the template's default
    messages (e.g. "Request completed successfully").
  - Three members kept on top of the template: `errorCode?`, `errors?` (field → messages, 400 only) and `traceId?` (errors).
    The error pipeline writes them through the `Error(statusCode, errorCode, message, errors, traceId, errorDetails)` overload.
  - Members that don't apply are omitted (attribute-level `WhenWritingNull`), so nulls *inside* `data` still serialise and DTO
    contracts don't change.
  - **`errorDetails`** holds the exception type and message of an unexpected 500, **in Development only**. It is always absent
    elsewhere, because exception text can carry connection strings, SQL or patient data (`StagingEnvironmentTests` asserts this).
  - **`timestamp`** is the server's UTC wall-clock time for support. It is not business time, so it is taken from
    `TimeProvider.System`.
  - `POST /auth/logout` stays **204 with no body**: an HTTP 204 cannot carry the envelope. `ApiResponse<T>.NoContent()` exists
    for template parity.
- **Controllers** return `IActionResult` (`OkResponse(...)` → `ApiResponse<T>.Success`, `ApiResponse<T>.Created(...)`) and declare every status they can
  return with `[ProducesResponseType(typeof(ApiResponse<…>), …)]`; 401 and 500 are declared once on `ApiControllerBase`.
  No `if (!ModelState.IsValid)` in actions: `[ApiController]` short-circuits invalid model state before the action runs and
  `BadRequestResponse.FromModelState` returns the same envelope (400 `BAD_REQUEST`), so the check would be dead code.
- **Exceptions** (`Domain/Common/SchedulerExceptions.cs`): abstract `SchedulerException(errorCode, message, inner?)` with
  `ErrorCode`, `abstract int StatusCode` and `LogDetail { init; }`. One subclass per status: `ValidationException` 400,
  `UnauthorizedException` 401, `ForbiddenException` 403, `NotFoundException` 404, `ConflictException` 409,
  `BusinessRuleException` 422, `LockedException` 423. The status is a plain `int`, so Domain still references nothing.
- **`LogDetail`** is log-only diagnostics (constraint names, token family ids, BR-7 doctor ids) — never serialised, ids only
  (NFR-9). `Message` stays safe to show users. `GlobalExceptionHandler` logs code + `LogDetail` at Information.
- **`ErrorCodes`** (`Domain/Common/ErrorCodes.cs`): every code as a `const`, UPPER_SNAKE, grouped by feature section; never a
  string literal at a throw site. `ErrorCodesTests` enforces format and uniqueness.
- **One writer** for responses that don't come from an action result — the exception handler, JWT `OnChallenge`/`OnForbidden`
  and framework-made errors (`UseStatusCodePages` → `IProblemDetailsService` → `ApiResponseProblemDetailsWriter`; MVC client
  errors → `ApiResponseClientErrorFactory`; codes `ROUTE_NOT_FOUND`, `METHOD_NOT_ALLOWED`, `UNSUPPORTED_MEDIA_TYPE` from
  `ErrorStatusMapping`): `Api/Errors/ApiErrorWriter`, using
  MVC's JSON options and `Activity.Current.Id` as `traceId`.
- **Angular**: `apiResponseInterceptor` (innermost) unwraps `data`, so services keep DTO types; `apiErrorInterceptor` shows
  `errors` → `message`; components branch on `error.errorCode`.

## Why 422 stays for business rules
400 means "we could not read or accept your input" (`BAD_REQUEST`, `VALIDATION_FAILED`); 422 means "valid request, but a rule
forbids it" (`APPOINTMENT_IN_PAST`, `INVALID_SLOT`). Clients can tell a form error from a rule rejection by status alone.

## Alternatives
Keep ProblemDetails (standard, but a second shape next to successes and not the house style); ProblemDetails for errors +
envelope for successes (two shapes again); status mapping in the handler instead of on the exception (the switch had to grow
with every new exception type).

## Consequences
+ One shape for clients and k6 scripts; codes are discoverable in one file; new exception types need no handler change;
  OpenAPI documents every status per action.
− Not the RFC 7807 standard — generic tooling that understands ProblemDetails won't parse our errors. The envelope repeats the
  HTTP status in the body. Codes are now UPPER_SNAKE: a breaking change for any client that matched the old lowercase values
  (only our Angular app, updated in the same commit).
− `ApiResponse<T>.Success(...)` and the other static factories on a generic type trip CA1000; suppressed in that file because the call-site
  readability is the point of the type.
