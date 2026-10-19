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
Open **http://localhost:4200**. API docs: **http://localhost:5080/scalar**.

On first start (Development) the API applies EF Core migrations and seeds fictional data:
4 doctors, 12 patients (`MRN-0001`…`MRN-0012`) and a few appointments **today and tomorrow** so the schedule isn't empty.
To reset: `docker compose down -v` and start again.

> Other infrastructure in `docker-compose.yml` (RabbitMQ, Jaeger) is for later milestones and not needed for the
> core app. Copy `.env.example` to `.env` to change ports/passwords (default Postgres port 5432).

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
| `Seed:Enabled` | `true` | Seed fictional data on an empty database |

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
