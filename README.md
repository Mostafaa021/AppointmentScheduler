# Clinic Appointment Scheduler

Book patients with doctors in 30-minute slots — **without double-booking**, even under concurrent requests.
.NET 10 · ASP.NET Core · EF Core · PostgreSQL · Angular 22.

- **[SPEC.md](SPEC.md)** — requirement, data model, APIs, where conflict checking lives, time zones, concurrency
- **[PROMPTS.md](PROMPTS.md)** — prompts used with Claude Code and what was accepted / changed / rejected
- [docs/](docs) — feature specs, ADRs, architecture diagrams, SDLC process, backlog

## Prerequisites
| Tool | Version |
|---|---|
| .NET SDK | 10.0.4xx (`global.json`) |
| Node.js / npm | 24 / 11 |
| Docker Desktop | running (PostgreSQL, and Testcontainers for integration tests) |

## Run it (3 terminals)
```bash
docker compose up -d postgres
```
```bash
dotnet run --project src/AppointmentScheduler.Api
```
```bash
cd web && npm install && npm start
```
Open **http://localhost:4200** and sign in. API docs: **http://localhost:5080/scalar**. Background jobs dashboard (Development,
local only): **http://localhost:5080/hangfire**.

**Demo users** (Development seed only, fictional), all with password `DemoClinic2026`:

| E-mail | Role | Sees |
|---|---|---|
| `admin@clinic.test` | Admin | everything, incl. user admin API |
| `reception@clinic.test` | Receptionist | full schedule: book / reschedule / cancel |
| `doctor@clinic.test` | Doctor (Dr. Amelia Hart) | own day schedule, read-only |

Use Chrome or Firefox locally: the refresh cookie is `Secure`, which Safari rejects on `http://localhost`.

On first start (Development) the API applies EF Core migrations and seeds fictional data:
4 doctors, 12 patients (`MRN-0001`…`MRN-0012`) and a few appointments **today and tomorrow** so the schedule isn't empty.
To reset: `docker compose down -v` and start again.

> `docker compose up -d postgres rabbitmq jaeger` also starts the tracing UI: **Jaeger** at http://localhost:16686
> (service `appointment-scheduler-api`; the API sends OTLP traces to `localhost:4317` in Development). RabbitMQ
> (management UI http://localhost:15672, login `guest` / `guest`) is used by messaging; the app also runs without it. Copy `.env.example` to `.env` to change ports/passwords (default Postgres port 5432).

## Using it
1. Pick a doctor and a date (times are shown in the clinic zone, Europe/London).
2. Click a **Free** slot → search a patient (e.g. "Nora" or `MRN-0002`) → reason → **Review** → **Confirm**.
3. In **Day schedule**: **Reschedule** (pick a new free slot; slots where the patient is busy elsewhere are marked) or **Cancel**.

## Tests
```bash
dotnet test
```
Runs unit (domain rules, DST), architecture (layer rules) and integration tests (real PostgreSQL via Testcontainers —
including 10 parallel bookings of one slot → exactly one succeeds). Docker must be running.
```bash
cd web && npm test -- --watch=false
```

## Configuration (`src/AppointmentScheduler.Api/appsettings.json`)
| Key | Default | Meaning |
|---|---|---|
| `ConnectionStrings:Scheduler` | localhost:5432, db `scheduler` | PostgreSQL |
| `Clinic:TimeZone` | `Europe/London` | IANA zone of the clinic |
| `Clinic:Open` / `Clinic:Close` / `Clinic:SlotMinutes` | `09:00` / `17:00` / `30` | Opening hours and slot size |
| `Seed:Enabled` | `true` | Seed fictional data on an empty database (demo users only in Development) |
| `Jwt:SigningKey` (env `Jwt__SigningKey` / `JWT_SIGNING_KEY`) | dev-only value in `appsettings.Development.json` | ≥ 32-byte HS256 key; never commit a real one |
| `BackgroundJobs:Enabled` | `true` | Run the Hangfire server and recurring jobs in the API process |
| `Idempotency:RecordTtlHours` / `InProgressTimeoutSeconds` | `24` / `60` | How long a key is remembered; when a stuck in-progress key may be taken over |
| `RateLimiting:Enabled` / `ConcurrentRequests` | `true` / `200` | Polly rate-limit pipelines on / global concurrency cap (ADR-0009) |
| `RateLimiting:Reads` / `Writes` / `Auth` | 300/min sliding · burst 10 + 1 per 2 s · 5/min per IP | Per-user (or IP) limits; 429 `RATE_LIMITED` + `Retry-After` |
| `Observability:ServiceName` | `appointment-scheduler-api` | OpenTelemetry service name (as shown in Jaeger) |
| `Observability:OtlpEndpoint` (env `Observability__OtlpEndpoint`) | empty; `http://localhost:4317` in Development | OTLP traces target (Jaeger); empty = no export |
| `Observability:MetricsEndpoint` | empty | OTLP metrics target (Jaeger has no metrics); empty = no export |
| `Messaging:RabbitMq:Enabled` / `Host` / `Port` / `UserName` / `Password` | `true` / `localhost` / `5672` / `guest` / `guest` in Development (local Docker only) | RabbitMQ for Wolverine; `Enabled=false` = PostgreSQL message store only |
| `Messaging:Publishing:Exchange` | `appointment-scheduler.events` | Topic exchange for integration events (declared at startup) |
| `Messaging:RecoveryIntervalMinutes` / `RestartOnRecovery` | `1` / true outside Development | Degraded mode: re-check the broker, then restart the host to attach it |

## Project layout
```
src/AppointmentScheduler.Domain          rules: Appointment, DaySchedule (no dependencies)
src/AppointmentScheduler.Application     use cases: AppointmentService, AvailabilityService, LookupQueries
src/AppointmentScheduler.Infrastructure  EF Core + Npgsql, migrations (exclusion constraints), seed
src/AppointmentScheduler.Api             controllers, global exception handler, auth, Hangfire host, OpenAPI
tests/                                   Unit, Architecture, Integration
web/                                     Angular app (features/schedule)
docs/                                    specs, ADRs, architecture, SDLC, backlog
.claude/                                 Claude Code agents (SDLC roles) and project skills
```

## Working on it with Claude Code
`CLAUDE.md` holds the project rules. Project skills: `/sdlc <feature>` (full role-agent ceremony, `--fast` for small
changes), `/new-feature`, `/add-migration`, `/verify`, `/load-test`, `/log-prompt`. See [docs/sdlc.md](docs/sdlc.md).
