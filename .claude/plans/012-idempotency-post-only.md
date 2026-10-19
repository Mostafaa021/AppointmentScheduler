# 012 — Idempotency-Key on POST only; `[SkipIdempotency]` per action

- Status: **Done** · Approved 2026-10-20 · Spec 009 · ADR-0006 (amendment)
- Progress: filter + OpenAPI ☑ · unit tests ☑ · Angular ☑ · docs ☑ · live check ☑ (commits only when the user says so)

## Context
Until now every POST/PUT/PATCH/DELETE needed an `Idempotency-Key` (spec 009). The user wants the key only where HTTP gives no
idempotency by design, which is **POST** (the user chose POST only). PUT, PATCH and DELETE need no key; today that frees
`PUT /users/{id}/roles`. Book, cancel and reschedule are POST actions, so they stay keyed.

Future POST endpoints that only *read*, such as a search with criteria in the body, must be exemptable one by one.
`[SkipIdempotency]` already allowed `Class | Method` and the filter read endpoint metadata, so it already worked on a single
action. It was simply not documented as the intended use, and nothing tested it.

## Changes
1. **`Api/Idempotency/IdempotencyFilter.cs`**: `IsWrite(method)` (POST/PUT/PATCH/DELETE) is replaced by one scope check,
   shared by the filter and the OpenAPI transformer:
   `internal static bool Applies(string method, IReadOnlyList<object> endpointMetadata) => HttpMethods.IsPost(method) && !metadata has [SkipIdempotency]`.
   Doc comments and the 400 message now say "every POST".
2. **`SkipIdempotencyAttribute.cs`**: the doc comment covers both uses:
   - auth on the controller;
   - per action for a POST that only reads (`[HttpPost("search")] [SkipIdempotency]`).
3. **`Api.csproj`**: `InternalsVisibleTo` for `AppointmentScheduler.UnitTests`.
4. **Unit test** `UnitTests/Idempotency/IdempotencyScopeTests.cs`:
   - POST applies;
   - PUT, PATCH, DELETE and GET don't;
   - a POST with `[SkipIdempotency]` in its metadata doesn't.
5. **Angular** `idempotency-key.interceptor.ts`: the key goes on `/api/` POSTs only, never under `/api/v1/auth/`. The spec
   gains a "PUT and DELETE get no key" case.
6. **Docs:**
   - spec 009 AC-009-1 and its scope line;
   - ADR-0006;
   - CLAUDE.md, SPEC.md, NFR-5;
   - the `/new-feature` skill and the backend agent;
   - CHANGELOG, PROMPTS.

## Verification
- `dotnet build -warnaserror`; `dotnet test`.
- `cd web && npm test -- --watch=false && npm run build`.
- **Live:**
  - POST without a key → 400 `IDEMPOTENCY_KEY_REQUIRED`;
  - `PUT /users/{id}/roles` without a key → 200;
  - OpenAPI shows the header on the four POSTs only.
