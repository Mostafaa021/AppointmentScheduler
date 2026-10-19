# 004 — Authentication, authorization & API hardening

| Status | Refined ☑ · Designed ☑ · Built ☐ · Tested ☐ · Reviewed ☐ · Released ☐ · Documented ☐ |
|---|---|
| Milestone | M2 (stories S-20, S-21, S-22, S-23) |
| Business rules touched | BR-7 — a user whose only role is Doctor can read only their own doctor's appointments and cannot change any |
| NFRs touched | NFR-4 (auth), NFR-9 (privacy), NFR-8 (traceId in audit), NFR-6 (deferred to M3, see NFR notes) |

> Status note 2026-10-19: Designed by architect (ADR-0011, 0012, 0013; ADR-0005 amended).
> Status note 2026-10-19 — Human approved design (FluentValidation per ADR-0013 rev.; decisions: 423 enumeration risk
> accepted for M2; AC-004-21 Playwright check deferred to M7, Vitest only in M2; simultaneous two-tab refresh → reuse/logout
> accepted, no grace window; AC-004-19 retried 401 → logout + `/login` confirmed).

## Goal
Only authenticated staff use the system, each role can do only its job, and every request is validated, hardened and audited.

## User story
As clinic staff (Admin, Receptionist, Doctor), I want to sign in and see only what my role allows, so that patient data and
the schedule are protected and every change can be traced to a person.

## Roles
| Role | Can |
|---|---|
| Admin | everything, incl. users, clinics, doctors, schedules |
| Receptionist | patients, availability, book / cancel / reschedule |
| Doctor | read own appointments only |

A user's permissions are the **union** of their roles. The BR-7 restriction applies to users whose only role is Doctor.

## Story → AC
| Story | AC |
|---|---|
| S-20 Identity + JWT + refresh rotation | AC-004-1..7, 10, 11..14, 26 |
| S-21 Role policies + Doctor resource check | AC-004-8, 9 |
| S-22 Angular login, guards, interceptor | AC-004-15..21 |
| S-23 Validation, security headers, audit | AC-004-22..25 |

All routes below are under `/api/v1`. Every error is RFC 7807 ProblemDetails with a stable `code` extension (SPEC.md §3).

## Acceptance criteria

### Authentication (S-20)
- **AC-004-1** Given an existing user, When `POST /auth/login {email, password}` with correct credentials, Then 200
  `{accessToken, expiresAt, user{id, email, roles, doctorId?}}` and a `Set-Cookie` refresh token with `HttpOnly; Secure;
  SameSite=Strict; Path=/api/v1/auth`. Given email or password missing/blank, Then 400 `validation_failed` with
  `errors.email` / `errors.password`.
- **AC-004-2** Given an unknown email or a wrong password, When login, Then 401 `invalid_credentials`; the response body is
  identical in both cases.
- **AC-004-3** Given a user with 4 consecutive failed logins, When the 5th consecutive attempt fails, Then 423
  `account_locked`; And for the next 15 min every login for that user, even with the correct password, → 423
  `account_locked`; And after 15 min the correct password → 200. A successful login resets the failure count.
- **AC-004-4** Given an access token, Then it expires 15 min after issue (`expiresAt`). When any `/api/v1` route other than
  `POST /auth/login` and `POST /auth/refresh` is called with no token, a malformed token, a token with an invalid signature /
  issuer / audience, or a token expired by more than 30 s, Then 401 `unauthorized` with header `WWW-Authenticate: Bearer`.
- **AC-004-5** Given a valid refresh cookie, When `POST /auth/refresh`, Then 200 with the same body shape as AC-004-1 (roles and
  `doctorId` read from the user's current data) and a new refresh cookie; the presented token is revoked. Each refresh token
  expires 7 days after issue. Given no cookie, an unknown, expired or logout-revoked token, Then 401 `invalid_refresh_token`.
- **AC-004-6** Given a refresh token that was already rotated (AC-004-5), When it is presented to `POST /auth/refresh`, Then
  401 `refresh_token_reused`; And every token of that family is revoked, so the newest token of the family → 401
  `invalid_refresh_token`.
- **AC-004-7** Given an authenticated user (valid bearer) with a refresh cookie, When `POST /auth/logout`, Then 204, the
  refresh token is revoked (a later refresh with it → 401 `invalid_refresh_token`) and the cookie is cleared (`Set-Cookie`
  expired). Without a refresh cookie → still 204. Without a valid bearer → 401 `unauthorized` (NFR-4). An already-issued access
  token stays valid until it expires (≤ 15 min).
- **AC-004-10** Given a valid access token, When `GET /me`, Then 200 `{id, email, roles, doctorId?}` of the caller. Given no
  token or an expired token, Then 401 `unauthorized`.

### User administration (S-20)
- **AC-004-11** Given an Admin, When `POST /users {email, password, roles[], doctorId?}` with valid data, Then 201
  `{id, email, roles, doctorId?}`; And the new user can log in (AC-004-1). A user with role Doctor is linked to `doctorId`.
- **AC-004-12** Given an Admin, When `POST /users` with any of: invalid e-mail format; a password that violates the password
  policy (values set in Design per ADR-0005); `roles` empty or containing a value other than `Admin`, `Receptionist`,
  `Doctor`; role `Doctor` without `doctorId`; `doctorId` without role `Doctor` — Then 400 `validation_failed` with the
  offending field(s) in `errors` (`email`, `password`, `roles`, `doctorId`) and no user is created.
- **AC-004-13** Given an Admin, When `POST /users` with a `doctorId` that does not exist, Then 404 `doctor_not_found`; When the
  e-mail already belongs to a user (case-insensitive), Then 409 `email_taken`. No user is created in either case.
- **AC-004-14** Given an Admin and an existing user, When `PUT /users/{id}/roles {roles[], doctorId?}` with valid data, Then
  200 `{id, email, roles, doctorId?}` and the stored `doctorId` becomes the given value (null when role Doctor is absent); the
  change applies to the user's next login or refresh (AC-004-5). Unknown user id → 404 `user_not_found`; unknown `doctorId` →
  404 `doctor_not_found`; the AC-004-12 rules for `roles` / `doctorId` → 400 `validation_failed`.

### Authorization (S-21)
- **AC-004-8** Given an authenticated user, When they call a route their roles do not allow (matrix below), Then 403
  `forbidden` and nothing changes. Precedence: missing/invalid token → 401 before any role check; role check → 403 before
  validation or lookups.

  | Route | Admin | Receptionist | Doctor |
  |---|---|---|---|
  | `POST /auth/login`, `POST /auth/refresh` | anonymous | anonymous | anonymous |
  | `POST /auth/logout`, `GET /me` | ✔ | ✔ | ✔ |
  | `GET /doctors`, `GET /doctors/{id}/availability`, `GET /patients` | ✔ | ✔ | 403 |
  | `GET /appointments?doctorId=&date=`, `GET /appointments/{id}` | ✔ | ✔ | own doctor only (AC-004-9) |
  | `POST /appointments`, `POST /appointments/{id}/cancel`, `POST /appointments/{id}/reschedule` | ✔ | ✔ | 403 |
  | `POST /users`, `PUT /users/{id}/roles` | ✔ | 403 | 403 |
- **AC-004-9** (BR-7, see AC-003-9) Given a user whose only role is Doctor, linked to doctor D: When `GET /appointments?doctorId=D&date=…`,
  Then 200; When `doctorId` ≠ D, Then 403 `forbidden`. When `GET /appointments/{id}` of an appointment of D, Then 200; of
  another doctor, Then 403 `forbidden`; of an unknown id, Then 404 `appointment_not_found`.

### Angular (S-22)
- **AC-004-15** Given an unauthenticated visitor, When they open `/login`, Then they see an e-mail and a password field with
  labels and a submit button that is disabled while the request is pending. On 200 the app navigates to `/schedule`. On 400,
  401 or 423 the app stays on `/login` and shows the server's ProblemDetails `detail` (existing problem interceptor behaviour).
- **AC-004-16** Given the app starts (first load or browser reload), When no access token is in memory, Then it makes one
  `POST /auth/refresh`; on 200 the requested route opens, otherwise any route other than `/login` redirects to `/login`.
- **AC-004-17** (AC-004-9, AC-003-9) Given a user whose only role is Doctor, When `/schedule` opens, Then it shows only their own
  doctor's day schedule (`doctorId` from the login/refresh `user`) with a date picker; no doctor picker, no slot grid and no
  book / cancel / reschedule controls are rendered; And the app sends no request to `/doctors`, `/patients`,
  `/doctors/{id}/availability` or any mutating appointment endpoint. Admin and Receptionist see the existing schedule page.
- **AC-004-18** Given a logged-in user, When the app calls any `/api/v1` route except `POST /auth/login` and
  `POST /auth/refresh`, Then the request carries `Authorization: Bearer <accessToken>`; requests to any other origin never do.
- **AC-004-19** Given a request (not `/auth/login` or `/auth/refresh`) that returns 401, Then the app makes exactly one
  `POST /auth/refresh` and retries the original request once with the new token; no error message is shown when the retry
  succeeds. Given N requests that return 401 concurrently, Then exactly one refresh call is made and all N are retried after
  it. A retried request that returns 401 again is not refreshed again (→ AC-004-20).
- **AC-004-20** Given refresh fails (any non-200) or the user clicks the always-visible **Log out** control (which calls
  `POST /auth/logout`), Then the in-memory access token and user are cleared and the app navigates to `/login`, even if the
  logout call fails.
- **AC-004-21** Given a login or refresh succeeds, Then the access token is never written to `localStorage`,
  `sessionStorage`, IndexedDB or a JavaScript-readable cookie (checked in Vitest via storage spies and in Playwright by
  inspecting storage after login).

### Validation, security headers, audit (S-23)
- **AC-004-22** Given a request body that fails validation, When it reaches any endpoint below, Then 400 with the same shape as
  today: `{type, title: "Validation failed", status: 400, detail, code: "validation_failed", errors: {<camelCaseField>: [messages]}}`,
  all failing fields reported at once, no state change and no audit record. Validation runs before lookups (404/409/422).
  Malformed input that cannot bind (unparseable JSON, wrong types, missing required query values) stays 400 `bad_request`.

  | Endpoint | Rules |
  |---|---|
  | `POST /auth/login` | `email`, `password` required (AC-004-1) |
  | `POST /appointments` | `doctorId` > 0, `patientId` > 0, `startUtc` required, `reason` 1–500 chars after trim (AC-002-6) |
  | `POST /appointments/{id}/cancel` | `reason` optional, ≤ 500 chars |
  | `POST /appointments/{id}/reschedule` | `startUtc` required |
  | `POST /users`, `PUT /users/{id}/roles` | AC-004-12 |
- **AC-004-23** Given any API response (success, error, 401/403, health), Then it carries `X-Content-Type-Options: nosniff`,
  `X-Frame-Options: DENY` and `Referrer-Policy: no-referrer`; And every `/api/v1` response also carries
  `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`. Given an environment other than Development, When
  served over HTTPS, Then `Strict-Transport-Security` with `max-age` ≥ 31536000 is present; in Development it is absent.
- **AC-004-24** Given one of the events below completes, Then exactly one audit record is persisted with `userId` (the acting
  user; null for a failed login with an unknown e-mail), `action`, `entityType`, `entityId`, `timestampUtc` (from
  `TimeProvider`) and `traceId` (the request's trace id). Rejected requests (4xx) create no `appointment.*` or `user.*` record.

  | Trigger | `action` | `entityType` / `entityId` |
  |---|---|---|
  | AC-004-1 | `auth.login_succeeded` | `User` / user id |
  | AC-004-2, AC-004-3 (each 401/423) | `auth.login_failed` | `User` / user id or null |
  | AC-004-7 | `auth.logout` | `User` / user id |
  | AC-004-6 | `auth.refresh_token_reused` | `RefreshTokenFamily` / family id (`userId` = family owner) |
  | Book (AC-002-1) | `appointment.booked` | `Appointment` / id |
  | Cancel (AC-003-2) | `appointment.cancelled` | `Appointment` / id |
  | Reschedule (AC-003-4) | `appointment.rescheduled` | `Appointment` / id |
  | AC-004-11 | `user.created` | `User` / new user id |
  | AC-004-14 | `user.roles_changed` | `User` / target user id |
- **AC-004-25** (NFR-9) Given the flows of AC-004-1..14 and AC-004-24 run with known test values, Then neither audit records
  nor log output contain any password, access token, refresh token, e-mail address, patient name or phone number.

### Demo users (S-20)
- **AC-004-26** Given the API runs in Development with the seed, Then three fictional users exist — one Admin, one
  Receptionist and one Doctor linked to a seeded doctor — and each can log in (AC-004-1) with credentials documented in
  README (chosen by the developer). Given any other environment, Then no demo user is seeded.

## API contract
| Method | Route | Auth | Request | Responses |
|---|---|---|---|---|
| POST | `/api/v1/auth/login` | anonymous | `{email, password}` | 200 · 400 · 401 · 423 |
| POST | `/api/v1/auth/refresh` | anonymous (refresh cookie) | — | 200 · 401 |
| POST | `/api/v1/auth/logout` | bearer | refresh cookie | 204 · 401 |
| GET | `/api/v1/me` | bearer | — | 200 · 401 |
| POST | `/api/v1/users` | Admin | `{email, password, roles[], doctorId?}` | 201 · 400 · 401 · 403 · 404 · 409 |
| PUT | `/api/v1/users/{id}/roles` | Admin | `{roles[], doctorId?}` | 200 · 400 · 401 · 403 · 404 |

`POST /users` returns 201 without `Location` (there is no `GET /users/{id}` until S-43).

New error codes: 401 `unauthorized`, `invalid_credentials`, `invalid_refresh_token`, `refresh_token_reused` · 403
`forbidden` · 404 `user_not_found` · 409 `email_taken` · 423 `account_locked`. All existing routes gain 401 and 403 (AC-004-8).

## Data changes
Confirmed by the architect (snake_case via the existing naming convention, `timestamptz` for all instants):

| Table | Columns / constraints |
|---|---|
| `identity.users` | Identity user columns (Guid `id`), `user_name` = e-mail, unique index on `normalized_user_name` (case-insensitive e-mail uniqueness backstop), + `doctor_id int null` FK → `public.doctors(id)` ON DELETE RESTRICT |
| `identity.roles` | Identity role columns; 3 rows seeded by migration with fixed ids: `Admin`, `Receptionist`, `Doctor` |
| `identity.user_roles`, `user_claims`, `user_logins`, `user_tokens`, `role_claims` | standard Identity tables, renamed into the schema (claims/logins/tokens unused) |
| `identity.refresh_tokens` | `id uuid` PK (v7) · `user_id uuid` FK → `identity.users` ON DELETE CASCADE · `token_hash bytea` (SHA-256, 32 bytes) UNIQUE · `family_id uuid` (index) · `created_at_utc` · `expires_at_utc` · `revoked_at_utc null` · `replaced_by_id uuid null` (no FK) |
| `public.audit_records` | `id uuid` PK (v7) · `user_id uuid null` · `action varchar(64)` · `entity_type varchar(64)` · `entity_id varchar(64) null` · `timestamp_utc` · `trace_id varchar(64) null` — no FKs, no extra indexes, insert-only ([ADR-0011](../adr/0011-audit-log-table.md)) |

Migrations (Infrastructure `Persistence/Migrations`): **`AddIdentityAndRefreshTokens`** (S-20: schema, Identity tables,
`doctor_id`, role seed, `refresh_tokens`) and **`AddAuditRecords`** (S-23).

## Design (architect)
Decisions: [ADR-0005](../adr/0005-identity-jwt.md) (amended 2026-10-19), [ADR-0011](../adr/0011-audit-log-table.md) audit
table, [ADR-0012](../adr/0012-br7-resource-check-in-application.md) BR-7 in the Application service,
[ADR-0013](../adr/0013-request-validation.md) FluentValidation validators run inside Application services.

**Shape.** The existing ArchitectureTests forbid `Microsoft.AspNetCore.*` in Domain and Application, and Identity types
(`IdentityUser`, `UserManager`) live in that namespace. So: **Application owns the contracts** (DTOs, `IAuthService`,
`IUserAdminService`, validators, `Roles`, `PasswordPolicy`, `ICurrentUser`, `AuditLog`), **Infrastructure implements them**
with Identity + `AppDbContext`, and **Api** stays thin (bind → service → map; cookie I/O; JWT challenge/forbid →
ProblemDetails; policies; headers). No new abstraction beyond those two service ports and `ICurrentUser`.

### Password policy (AC-004-12)
**12–128 characters, at least one lowercase letter, one uppercase letter and one digit; no symbol requirement.**
Constants in `Application/Auth/PasswordPolicy.cs`; `CreateUserRequestValidator` enforces them (all violations in `errors.password`) and
Infrastructure copies the same constants into `IdentityOptions.Password` (`RequiredLength = 12`, `RequireLowercase`,
`RequireUppercase`, `RequireDigit` = true, `RequireNonAlphanumeric = false`, `RequiredUniqueChars = 1`) as a backstop. The
128 maximum (bounds hashing cost) is validator-only.

### Domain
| File | Change |
|---|---|
| `Common/DomainExceptions.cs` | add `UnauthorizedException(code, message)` → 401, `ForbiddenException()` → 403 `forbidden`, `LockedException(code, message)` → 423 (same pattern as the existing types) |
| `Common/AuditRecord.cs` | new entity: `Id` (`Guid.CreateVersion7()`), `UserId?`, `Action`, `EntityType`, `EntityId?`, `TimestampUtc`, `TraceId?`; ctor only, no setters used after creation |

### Application
| File | Change |
|---|---|
| `Common/ICurrentUser.cs` | port: `Guid? UserId`, `IReadOnlyCollection<string> Roles`, `int? DoctorId`, `string? TraceId` (implemented in Api) |
| `Common/AuditLog.cs` | scoped class (`IAppDbContext`, `ICurrentUser`, `TimeProvider`): `Record(action, entityType, entityId)` uses the current user; overload `Record(action, entityType, entityId, Guid? actingUserId)` for login events. Only **adds** the row — the caller's single `SaveChangesAsync` commits it with the change. `AuditActions` constants (`auth.login_succeeded` … `user.roles_changed`) in the same file |
| `Common/ValidationExtensions.cs` | `ValidateOrThrow<T>(this IValidator<T>, T)`: synchronous `Validate`; on failure throws the existing domain `ValidationException` with failures grouped by camelCase top-level property (`Roles[1]` → `roles`) — response shape unchanged (ADR-0013) |
| `Common/IAppDbContext.cs` | + `DbSet<AuditRecord> AuditRecords` |
| `Auth/Roles.cs` | `Admin`, `Receptionist`, `Doctor`, `All` — used by validators, policies, seeding |
| `Auth/PasswordPolicy.cs` | constants above (used by the validator and by `IdentityOptions`) |
| `Auth/AuthDtos.cs` | `LoginRequest(Email?, Password?)`, `UserDto(Id, Email, Roles, DoctorId?)`, `AuthResponse(AccessToken, ExpiresAt, User)`, `AuthResult(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt)`, `CreateUserRequest(Email?, Password?, Roles?, DoctorId?)`, `SetUserRolesRequest(Roles?, DoctorId?)` |
| `Auth/AuthValidators.cs` | `LoginRequestValidator` (both `NotEmpty`, i.e. non-blank) · `CreateUserRequestValidator`: e-mail `NotEmpty` + `EmailAddress()` + ≤ 256; password `MinimumLength(12)`, `MaximumLength(128)`, `Matches` lowercase / uppercase / digit (default cascade → all violations reported) · `SetUserRolesRequestValidator` · both user validators `Include(new RolesRules())`: `roles` non-empty ⊆ `Roles.All`, Doctor ⇔ `doctorId` (error on `doctorId`), `doctorId` > 0 |
| `Auth/IAuthService.cs` | `LoginAsync(LoginRequest)`, `RefreshAsync(string? refreshToken)` → `AuthResult`; `LogoutAsync(string? refreshToken)`; `GetMeAsync()` → `UserDto` |
| `Auth/IUserAdminService.cs` | `CreateAsync(CreateUserRequest)`, `SetRolesAsync(Guid id, SetUserRolesRequest)` → `UserDto` |
| `Appointments/AppointmentValidators.cs` | `BookAppointmentRequestValidator` (`doctorId` > 0, `patientId` > 0, `startUtc` `NotNull`, `reason` 1–500 after trim via `Must`), `CancelAppointmentRequestValidator` (≤ 500), `RescheduleAppointmentRequestValidator` (`startUtc` `NotNull`) |
| `Appointments/AppointmentDtos.cs` | `StartUtc` becomes `DateTimeOffset?` in `BookAppointmentRequest` and `RescheduleAppointmentRequest` so "missing" is detectable (today a missing value binds to `0001-01-01` → misleading 422) |
| `Appointments/ScheduleAccess.cs` | BR-7: `EnsureCanRead(this ICurrentUser, int doctorId)` — if roles are exactly `[Doctor]` and `DoctorId != doctorId` → `ForbiddenException` (null `DoctorId` fails closed) |
| `Appointments/AppointmentService.cs` | ctor + `ICurrentUser`, `AuditLog`, the three `IValidator<T>`. Each public method calls `ValidateOrThrow` **first** (before `EnsureExistsAsync`/`FindAsync`); `GetDayScheduleAsync` calls `EnsureCanRead(doctorId)` before querying; `GetAsync` loads (404) then `EnsureCanRead(a.DoctorId)` (403); Book/Cancel/Reschedule call `audit.Record("appointment.…", "Appointment", id)` before their single `SaveChangesAsync` |
| `DependencyInjection.cs` | + `AddScoped<AuditLog>()`; the 6 validators registered explicitly as singleton `IValidator<T>` (no DI-extensions package, ADR-0013) |

`LookupQueries` and `AvailabilityService` are unchanged (role policies cover them).

### Infrastructure
| File | Change |
|---|---|
| `Auth/AppUser.cs` | `AppUser : IdentityUser<Guid>` + `int? DoctorId` |
| `Auth/RefreshToken.cs` | entity matching `identity.refresh_tokens` (Infrastructure-only; Application never sees it) |
| `Auth/JwtOptions.cs` | section `Jwt`: `Issuer`, `Audience`; `SigningKey` bound from configuration key **`JWT_SIGNING_KEY`** (env var in compose/prod, dev value in `appsettings.Development.json`, random per run in tests). `ValidateOnStart`: key ≥ 32 UTF-8 bytes, issuer/audience non-empty → the API refuses to start otherwise. Lifetimes are constants, not config: access 15 min, refresh 7 days, skew 30 s |
| `Auth/TokenIssuer.cs` | `JsonWebTokenHandler` + HS256 `SymmetricSecurityKey`; claims `sub`, `role` (one per role), `doctor_id` (if set), `iss`, `aud`, `iat`/`nbf`/`exp` from `TimeProvider`. Refresh token = 32 bytes `RandomNumberGenerator` → base64url; stored as SHA-256 bytes |
| `Auth/IdentityAuthService.cs` | `IAuthService` using `UserManager<AppUser>`, `AppDbContext`, `TokenIssuer`, `AuditLog`, `TimeProvider`, `IValidator<LoginRequest>` — flows below |
| `Auth/IdentityUserAdminService.cs` | `IUserAdminService` (injects both user validators): `ValidateOrThrow` → (Set: user exists else 404 `user_not_found`) → doctor exists else 404 `doctor_not_found` → (Create: `FindByEmailAsync` → 409 `email_taken`) → in **one transaction**: `CreateAsync`/`AddToRolesAsync` (or remove+add roles, set `DoctorId`, `null` when Doctor absent) + `audit.Record("user.created" / "user.roles_changed")`. An Identity `DuplicateUserName/DuplicateEmail` result → 409 `email_taken`; any other failed `IdentityResult` is a bug → exception (500) |
| `Persistence/AppDbContext.cs` | inherits `IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>` (calls `base.OnModelCreating` first); + `RefreshTokens`, `AuditRecords`; `SaveChangesAsync` additionally maps unique violation (23505) on `normalized_user_name` → `ConflictException("email_taken")` (race backstop) |
| `Persistence/Configurations/IdentityConfiguration.cs` | Identity tables → `identity` schema with short names; `AppUser.DoctorId` FK; role `HasData` (fixed Guids + concurrency stamps) |
| `Persistence/Configurations/RefreshTokenConfiguration.cs`, `AuditRecordConfiguration.cs` | per Data changes |
| `Persistence/Seed/DbSeeder.cs` | `SeedAsync(…, bool includeDemoUsers)`: after the existing data, if `includeDemoUsers` and no users exist, insert 3 fictional users (`*@clinic.test`, Admin / Receptionist / Doctor → first seeded doctor) with `new PasswordHasher<AppUser>()`, upper-invariant normalized names, security stamps and `user_roles` rows. Credentials are constants here and in README (AC-004-26) |
| `DependencyInjection.cs` | `includeDemoUsers = seed && sp.GetRequiredService<IHostEnvironment>().IsDevelopment()` (AC-004-26: never outside Development, even with `Seed:Enabled=true`); `AddIdentityCore<AppUser>(o ⇒ password policy, RequireUniqueEmail).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>()`; `JwtOptions`; `AddAuthentication(JwtBearer).AddJwtBearer` with `TokenValidationParameters`: issuer, audience, signing key, `ValidAlgorithms = [HS256]`, `RequireExpirationTime`, `MapInboundClaims = false`, `NameClaimType = "sub"`, `RoleClaimType = "role"`, and a **`LifetimeValidator` using `TimeProvider`** (`now ≤ exp + 30 s`, `nbf − 30 s ≤ now`) because IdentityModel's default reads the system clock; registers `TokenIssuer`, both services |

#### Login and lockout (AC-004-1..3)
1. `ValidateOrThrow` → 400.
2. `FindByEmailAsync`. Unknown → verify the password against a static dummy hash (equal timing), audit
   `auth.login_failed` (user and entity null), save → 401 `invalid_credentials`.
3. **Reserve an attempt atomically:** `ExecuteUpdate users SET access_failed_count = access_failed_count + 1 WHERE id = @id
   AND access_failed_count < 5 AND (lockout_end IS NULL OR lockout_end <= @now)`. 0 rows → audit failed, save → 423
   `account_locked` (also with the correct password).
4. Verify with `IPasswordHasher<AppUser>.VerifyHashedPassword` (not `CheckPasswordAsync`, whose rehash path calls
   `UpdateAsync` and would write the stale tracked counter back over step 3). Success → `ExecuteUpdate` count = 0, lockout_end = null; insert refresh token
   (new `family_id`); audit `auth.login_succeeded`; save → 200 + cookie.
5. Failure → re-read count; ≥ 5 → `ExecuteUpdate` lockout_end = now + 15 min, count = 0 → 423; else 401. Audit
   `auth.login_failed` with the user id either way.

The `< 5` guard caps a parallel burst at 5 guesses per lockout window (a read-modify-write counter would let N parallel
guesses count as 1). All times come from `TimeProvider`.

#### Refresh rotation and reuse (AC-004-5, 6)
Hash the cookie value → find by `token_hash`, then in this order: not found → 401 `invalid_refresh_token`;
`replaced_by_id` set (already rotated) → **reuse**: in one transaction revoke every unrevoked token of the family, audit
`auth.refresh_token_reused` (`RefreshTokenFamily` / family id, user = owner), commit → 401 `refresh_token_reused`; revoked
or expired → 401 `invalid_refresh_token`; otherwise in one transaction **claim** it with `ExecuteUpdate … SET
revoked_at_utc = now, replaced_by_id = @newId WHERE id = @id AND revoked_at_utc IS NULL AND replaced_by_id IS NULL`
(0 rows → concurrent rotation → take the reuse path), insert the new token (same family, expires now + 7 days), reload the
user's roles and `doctor_id`, issue the access token. No audit on a successful refresh (not in AC-004-24).

#### Logout (AC-004-7)
Requires bearer (default policy). If a cookie is present: `ExecuteUpdate` revoke where `token_hash` matches **and**
`user_id` = caller and not yet revoked. Always audit `auth.logout`, save, 204, delete cookie.

### Api
| File | Change |
|---|---|
| `Endpoints/AuthEndpoints.cs` | `POST /auth/login`, `POST /auth/refresh` (`.AllowAnonymous()`), `POST /auth/logout`, `GET /me`. Reads/writes cookie **`refresh_token`**: `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/v1/auth`, `Expires` = refresh expiry; logout uses `Cookies.Delete` with the same path/flags |
| `Endpoints/UserEndpoints.cs` | `/users` group `.RequireAuthorization(Policies.AdminOnly)`: `POST /` → 201 (no Location), `PUT /{id:guid}/roles` → 200 |
| `Endpoints/LookupEndpoints.cs` | `/doctors` and `/patients` groups `.RequireAuthorization(Policies.FrontDesk)` |
| `Endpoints/AppointmentEndpoints.cs` | group `.RequireAuthorization(Policies.ScheduleReader)`; the three POSTs additionally `.RequireAuthorization(Policies.FrontDesk)` (policies AND-combine → Doctor gets 403) |
| `Auth/Policies.cs` | `AdminOnly` = Admin · `FrontDesk` = Admin, Receptionist · `ScheduleReader` = Admin, Receptionist, Doctor (`RequireRole`, i.e. union of roles) |
| `Auth/AuthSetup.cs` | `AddApiAuth()`: `AddAuthorizationBuilder()` with the 3 policies; `PostConfigure<JwtBearerOptions>` events — `OnChallenge`: `HandleResponse()`, 401, header `WWW-Authenticate: Bearer`, ProblemDetails `code: "unauthorized"`; `OnForbidden`: 403 ProblemDetails `code: "forbidden"` — both written via `IProblemDetailsService` (same shape as `DomainExceptionHandler`); registers `HttpContextAccessor` + `ICurrentUser` |
| `Auth/HttpCurrentUser.cs` | `ICurrentUser` from `HttpContext.User` (`sub`, `role`, `doctor_id`); `TraceId = Activity.Current?.TraceId.ToHexString() ?? HttpContext.TraceIdentifier` (= the trace id in logs/Jaeger) |
| `Security/SecurityHeaders.cs` | `UseSecurityHeaders()`: inline middleware registering `Response.OnStarting` → `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` on every response; plus `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` when the path starts with `/api/v1` |
| `Errors/DomainExceptionHandler.cs` | + `UnauthorizedException` → 401, `ForbiddenException` → 403, `LockedException` → 423 |
| `Program.cs` | `AddApiAuth()`, `AddHsts(o ⇒ MaxAge = 365 days)`. Pipeline order: `UseSecurityHeaders` (first, so error/401/403 responses get headers) → `UseExceptionHandler` → `UseStatusCodePages` → `UseHsts` when not Development → `UseCors` → `UseAuthentication` → `UseAuthorization`. Group: `MapGroup("/api/v1").RequireAuthorization()` (default = authenticated) then `MapAuthEndpoints().MapUserEndpoints().MapLookupEndpoints().MapAppointmentEndpoints()`. `/health/live`, `/openapi`, `/scalar` stay outside the group (anonymous) |
| `appsettings.json` / `appsettings.Development.json` | `Jwt: { Issuer: "appointment-scheduler", Audience: "appointment-scheduler-web" }`; Development adds a clearly dev-only `JWT_SIGNING_KEY` (`.env.example` already documents it) |

Precedence (AC-004-8) comes from the pipeline: authentication → authorization (role) run before endpoint binding, so 401
→ 403 → 400/404/409/422 → BR-7 403. `UseHsts` excludes `localhost` by default, so the AC-004-23 non-Development test must
use an `https://` base address with another host name.

**Secure cookie on `http://localhost`.** The flag stays on in every environment (no dev toggle). Chromium and Firefox treat
`http://localhost` as a secure context and store/send `Secure` cookies; the Angular dev server proxies `/api` from the same
origin (`localhost:4200`), so the host-only cookie works. Safari (WebKit) does not → local dev in Safari is unsupported. .NET's
`CookieContainer` won't send `Secure` cookies over `http://`, so integration tests pass the cookie explicitly (below).

**Error codes → source.** `invalid_credentials`, `invalid_refresh_token`, `refresh_token_reused` → `UnauthorizedException`;
`account_locked` → `LockedException`; `forbidden` → `ForbiddenException` (BR-7) or `OnForbidden` (role); `unauthorized` →
`OnChallenge`; `email_taken` → `ConflictException`; `user_not_found`, `doctor_not_found` → `NotFoundException`. `detail` texts
never echo the e-mail; `invalid_credentials` uses one fixed text for both cases (AC-004-2).

### Tests impact
- **`ApiFactory`**: `UseSetting("JWT_SIGNING_KEY", <random ≥ 32 chars per run>)`; after start, creates test users via
  `UserManager` (`admin@test.local`, `reception@test.local`, one shared `TestPassword` constant that meets the policy) and
  `CreateDoctorUserAsync(int doctorId)` for BR-7 tests. **`Task<HttpClient> ClientForAsync(email)`** logs in through
  `POST /api/v1/auth/login` (the real pipeline — no test-only JWT issuer) and sets `DefaultRequestHeaders.Authorization`.
  Adds a capturing `ILoggerProvider` for AC-004-25 log assertions.
- **Existing tests** (`Availability`, `Booking`, `ManageSchedule`): implement `IAsyncLifetime` and set
  `_client = await api.ClientForAsync(TestUsers.Receptionist)` instead of `api.CreateClient()`; no assertion changes.
  `ExclusionConstraintTests` uses the DbContext directly — unchanged.
- Tokens follow the fake clock (fixed at 08:00). Tests that `Clock.Advance` (lockout 15 min, token expiry > 30 s, refresh
  expiry 7 days) run in their own collection with their own `ApiFactory`, so the shared clock and other tests' tokens are
  unaffected.
- Cookie tests create clients with `HandleCookies = false`, read `Set-Cookie` and send `Cookie: refresh_token=…` explicitly
  (also how AC-004-6 presents a rotated token).
- One sweep test enumerates `EndpointDataSource` and asserts every `/api/v1` endpoint except login/refresh carries
  authorization metadata — a new endpoint without a policy fails the build.
- Unit tests (`UnitTests` already references Application): each validator, `ValidateOrThrow` key mapping (camelCase, `roles`),
  `ScheduleAccess`.
- **ArchitectureTests:** no rule blocks FluentValidation in Application. One addition: `"FluentValidation"` joins the
  Domain rule's forbidden list so validators never move into Domain. The existing "Application has no
  `Microsoft.AspNetCore` dependency" rule is what keeps Identity in Infrastructure; the design passes it.

### Angular (`web/src/app`)
| File | Change |
|---|---|
| `core/api/models.ts` | + `AuthUser {id, email, roles, doctorId?}`, `AuthResponse {accessToken, expiresAt, user}` |
| `core/auth/auth.service.ts` | `providedIn: 'root'`. Private `token = signal<string \| null>`, public readonly `user` signal, `isAuthenticated`, `isDoctorOnly = computed(roles is exactly ['Doctor'])`. `login()`, `refresh()` (**single-flight**: one shared in-flight observable, cleared on completion), `logout()` (POST then — in `finalize`, also on error — `clear()` + navigate `/login`), `clear()`, `restore()` for startup. Token lives only in memory (AC-004-21) |
| `core/auth/auth.interceptor.ts` | Adds `Authorization: Bearer` only to **relative** URLs starting with `/api/v1/`, except `/api/v1/auth/login` and `/auth/refresh` (absolute/other-origin URLs never). On 401 (not login/refresh, not already retried — `HttpContextToken RETRIED`): if the token changed since the request was sent, retry with the current one (no refresh — keeps "exactly one refresh" for late 401s); else `auth.refresh()` then retry once. Refresh failure, or a retried request that 401s again → `auth.clear()` + navigate `/login`, error rethrown |
| `core/http/problem.interceptor.ts` | + exported `HttpContextToken SILENT` → skip the snackbar (used by refresh and logout calls, so the startup refresh on the login page shows nothing) |
| `core/auth/auth.guards.ts` | `authGuard` (`canActivate`: authenticated or `UrlTree('/login')`); `doctorOnlyMatch` (`canMatch`: `isDoctorOnly()`) — the only role guard needed, since no route is role-restricted in the UI |
| `features/auth/login-page.ts` | standalone, Material form: labelled e-mail + password, submit disabled while `pending()`; 200 → navigate `/schedule`; 400/401/423 stay and rely on the existing problem snackbar (AC-004-15) |
| `features/schedule/doctor-schedule-page.ts` | **new component** (not a mode of `SchedulePage`): date picker (prev/next/today) + read-only day list from `SchedulerApi.daySchedule(user.doctorId, date)` only. `SchedulePage` loads `/doctors` in a field initializer, so reusing it would violate AC-004-17. The ~20-line list template is duplicated on purpose; extract a shared list component when a third view needs it |
| `app.routes.ts` | `login` → `LoginPage`; `schedule` twice: first `{canMatch: [doctorOnlyMatch], DoctorSchedulePage}` (doctor-only implies signed in), then `{canActivate: [authGuard], SchedulePage}`; `''` / `**` → `schedule` (guarded → `/login` when anonymous) |
| `app.config.ts` | `withInterceptors([problemInterceptor, authInterceptor])` — **problem outermost**, so a 401 that the auth interceptor recovers from never reaches the snackbar; `provideAppInitializer(() => inject(AuthService).restore())` → exactly one `POST /auth/refresh` (SILENT) at start; resolves on success or failure, so guards are synchronous |
| `app.ts` | toolbar shows a **Log out** button whenever `auth.user()` is set → `auth.logout()` |

Vitest: `auth.service.spec` (storage spies on `localStorage`/`sessionStorage`/`indexedDB`/`document.cookie`),
`auth.interceptor.spec` (header only same-origin; N concurrent 401 → 1 refresh; retry once), `login-page.spec`,
`doctor-schedule-page.spec` (`HttpTestingController.expectNone` for `/doctors`, `/patients`, availability, mutations).

### Packages
| Package | Where | Why |
|---|---|---|
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.13 | Infrastructure | EF Core stores for Identity (ADR-0005); not in the shared framework (`Identity.Core/Stores` are) |
| `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.13 | Infrastructure (flows to Api) | bearer validation; brings `Microsoft.IdentityModel.JsonWebTokens` used to issue tokens, so no third JWT package |
| `FluentValidation` 12.x (12.1.1 in the local cache) | Application | request validators (ADR-0013, human decision); `FluentValidation.DependencyInjectionExtensions` deliberately not added — 6 explicit registrations |

No npm package. AC-004-21's Playwright check is **deferred to M7** (human decision); M2 covers AC-004-21 with Vitest
storage spies only, so `@playwright/test` is not added now.

### Role matrix check (AC-004-8 vs code today)
All 8 existing `/api/v1` routes (`GET /doctors`, `GET /doctors/{id}/availability`, `GET /patients`, `GET /appointments`,
`GET /appointments/{id}`, `POST /appointments`, `/cancel`, `/reschedule`) appear in the matrix, and no matrix row names a
missing route besides the 6 new ones. **No mismatch.** Notes: `/health/live`, `/openapi`, `/scalar` are outside `/api/v1`
and stay anonymous (NFR-4). Side effect for AC-004-17: the Doctor view cannot show the clinic zone (it comes from the
forbidden availability endpoint); `localStart`/`localEnd` on each appointment are enough.

### Risk positions
- **Account enumeration via 423 — ACCEPTED for M2 (human, 2026-10-19).** Only existing users can lock, so 5 wrong attempts reveal
  whether an e-mail exists, and an attacker can keep a known staff account locked (DoS). Accepted because: it is an internal
  staff app; each probe is noisy (5 attempts, audited `auth.login_failed`) and locks the real account; the 401 body and
  timing are otherwise identical; M3's `auth` rate limit (5/min/IP, S-32) slows it further. Alternative if rejected: return
  401 `invalid_credentials` while locked (needs an AC change).
- **Strict reuse detection — ACCEPTED (human, 2026-10-19), no grace window.** It logs out every tab if two tabs reload at the same instant with the same cookie (second refresh
  = reuse). Accepted; a grace window would weaken theft detection.
- **AC-004-19 — CONFIRMED (human, 2026-10-19):** a retried request that 401s again ends the session like AC-004-20 (clear + `/login`).
- `refresh_tokens` rows are never purged in M2 (a purge job can join the M3 hourly idempotency purge).

**Approved by human 2026-10-19**

## NFR notes
- **Auth / roles (NFR-4)** — `/api/v1` group requires an authenticated user; login/refresh `AllowAnonymous`; policies
  `AdminOnly` (`/users`), `FrontDesk` (`/doctors`, `/patients`, appointment POSTs), `ScheduleReader` (appointment GETs) +
  BR-7 check in `AppointmentService` (ADR-0012); lockout 5 / 15 min via `TimeProvider` (ADR-0005 amendment). An endpoint
  sweep test guards against unprotected new routes.
- **Idempotency (NFR-5, M3)** — `/auth/*` is exempt from `Idempotency-Key` (AC-009-1); `POST /users` and
  `PUT /users/{id}/roles` get the filter in M3 (S-30) like every other mutating route. Nothing in this design blocks it.
- **Rate limiting (NFR-6 / ADR-0009)** — the `auth` policy on login/refresh is **deferred to M3 (S-32)**; in M2 the
  brute-force control is the lockout, with the atomic attempt reservation capping parallel guesses. No rate-limit AC here.
- **Observability (NFR-8)** — audit rows carry the W3C trace id (`Activity.Current.TraceId`, fallback
  `HttpContext.TraceIdentifier`). Log, with ids only: login failed (Information, `UserId` or none), lockout started and
  `refresh_token_reused` (Warning, `UserId`, `FamilyId`), user created / roles changed (Information, actor + target id).
  Never log e-mail, password, access or refresh token, cookie or `Authorization` header; keep EF sensitive-data logging
  and IdentityModel `ShowPII` at their defaults (off). New metrics wait for ADR-0010 in M3.
- **Time zones (NFR-11)** — token `iat`/`nbf`/`exp`, refresh `created/expires/revoked_at_utc`, `lockout_end`, cookie
  `Expires` and audit `timestamp_utc` all come from the injected `TimeProvider` and are UTC `timestamptz`; token lifetime is
  validated against `TimeProvider` too, so `FakeTimeProvider` drives every expiry test.
- **PII (NFR-9)** — e-mail is stored only in `identity.users` (needed to log in) and returned only in auth/user responses;
  audit rows hold ids, never e-mail; problem `detail` never echoes input; refresh tokens stored as SHA-256 hashes;
  passwords only as Identity hashes; demo users use fictional `*.test` addresses (AC-004-25, AC-004-26).

## UI notes
`/login` page (AC-004-15), toolbar **Log out** control (AC-004-20), Doctor read-only schedule (AC-004-17). Loading state:
submit disabled while pending; errors via the existing problem interceptor. Login fields use `<mat-label>` and
`autocomplete="username"` / `"current-password"`; the Doctor page reuses the existing date controls' labels and an empty
state ("No appointments.").

## Out of scope
SSO/OIDC, MFA, password reset / change and e-mail verification · listing, editing, deactivating or deleting users (Admin UI,
S-43) · protection against removing the last Admin · revoking already-issued access tokens on logout or role change (they
expire within 15 min) · `auth` rate-limit policy (M3, S-32) · reading, exporting or retaining audit records (no audit API/UI)
· security headers on the Angular static host (S-61) · returning to the originally requested URL after login · idempotency
keys (M3, S-30).
