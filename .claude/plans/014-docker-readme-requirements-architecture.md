# 014 — Run-anywhere Docker setup, README, requirements docs, per-feature architecture diagrams

- Status: **In progress** · Approved 2026-10-20 · Backlog S-61 (part) · all specs, ADR index
- Progress: Step A (Docker + README) ☑ · Step B (requirements + architecture) ☐ (commits only when the user says so)

## Context
You asked for three things:
1. A README explaining how the project works, with steps so anyone can bring it up with Docker Compose.
2. Documentation for every functional and non-functional requirement.
3. Architecture documentation with Mermaid diagrams for each feature.

Today `docker-compose.yml` starts only PostgreSQL, RabbitMQ and Jaeger; the API and the Angular app need .NET 10 and Node 24
installed locally. `docs/architecture.md` is partly stale:
- a dangling `K` node left from the removed Worker;
- "nginx", which doesn't exist yet;
- the notification flow, which is planned, is drawn as if it were built.

The requirements live spread across specs 000–009 and SPEC.md.

Your choices:
- full Docker (Dockerfiles plus `api` and `web` services; this is backlog S-61);
- two requirement catalogues;
- built and planned features both covered (planned ones marked);
- one architecture file per feature, with `docs/architecture.md` kept as the overview.

Two steps; I stop after each and ask whether to commit or leave the work in the working tree. The plan is saved as
`.claude/plans/014-docker-readme-requirements-architecture.md`.

## Step A — Full stack in Docker + README
- **`src/AppointmentScheduler.Api/Dockerfile`**, a multi-stage build with the repository root as context:
  - stage 1 uses `sdk:10.0`: copy `global.json`, `Directory.*.props` and the four `src` csproj files, `dotnet restore`
    (cached layer), then copy `src/` and `dotnet publish -c Release`;
  - stage 2 uses `aspnet:10.0` (Debian, so tzdata for `Europe/London` and ICU are present), with `ASPNETCORE_HTTP_PORTS=8080`
    and `ENTRYPOINT dotnet AppointmentScheduler.Api.dll`.
- **`web/Dockerfile`** + **`web/nginx.conf`**:
  - `node:24-alpine` runs `npm ci` and `npm run build`; then `nginx:1.27-alpine` serves `dist/web/browser`;
  - nginx falls back to `index.html` for the SPA and proxies `/api/` to `http://api:8080`, so it is same-origin: no CORS,
    and the refresh cookie path `/api/v1/auth` works. Chrome and Firefox treat `http://localhost` as secure, so the `Secure`
    cookie works.
- **`.dockerignore`** (root) and **`web/.dockerignore`**: `bin/`, `obj/`, `node_modules/`, `dist/`, `.angular/`, `.git/`,
  `tests/`, `docs/`, `.claude/`.
- **`docker-compose.yml`** gains two services; the existing three are unchanged, so
  `docker compose up -d postgres rabbitmq jaeger` still works for local development.
  - `api`: build from the Dockerfile above, `ports 5080:8080`, and `ASPNETCORE_ENVIRONMENT: Development`.
    - Development is commented as a **local demo stack**: it migrates on start, seeds demo users and serves `/scalar`.
    - `ConnectionStrings__Scheduler` points at `postgres` and uses the `POSTGRES_*` variables.
    - `Observability__OtlpEndpoint=http://jaeger:4317`.
    - `Messaging__RabbitMq__Host=rabbitmq`, with its user and password from `RABBITMQ_*`.
    - `depends_on`: postgres and rabbitmq `service_healthy`, jaeger started.
  - `web`: build `./web`, `ports 8080:80`, depends on api.
- **`.env.example`**: drop the duplicated RabbitMQ comment, and note which variables Docker vs. local runs use.
- **README.md** rewritten for a newcomer:
  - **What it is / how it works**: a short paragraph plus one Mermaid container diagram (browser → nginx → API →
    Postgres/RabbitMQ/Jaeger), and a "request life" summary.
  - **Quick start (Docker only)**: prerequisites (Docker Desktop, about 4 GB free), clone, optional `cp .env.example .env`,
    `docker compose up --build -d`, `docker compose ps` until healthy, then open http://localhost:8080. Demo users. The first
    build takes a few minutes.
  - **URLs table**: app :8080, API + Scalar :5080, Jaeger :16686, RabbitMQ :15672 (guest/guest), Postgres :5432.
  - **Everyday commands**: logs (`docker compose logs -f api`), stop, reset data (`down -v`), rebuild after changes.
  - **Local development (option B)**: infrastructure in Docker, plus `dotnet run` and `npm start`. Stop the `api`/`web`
    containers first, because of the ports.
  - **Troubleshooting**: ports in use; Safari and the Secure cookie; RabbitMQ down → degraded mode; `/hangfire` only in
    local-run mode, since its dashboard allows local requests only.
  - **Using it**, **Tests**, **Configuration** (existing table, kept), **Project layout**, and a **Documentation map**
    pointing at the requirement catalogues, the architecture overview and the feature files, specs and ADRs.
- **Claude files**:
  - CLAUDE.md Commands: add a "Full stack in Docker" row;
  - devops-engineer agent: Dockerfiles and compose services exist;
  - backlog S-61 → "Dockerfiles + full compose done; migrator, operations.md and ForwardedHeaders still Todo".
- **Not in scope** (named in the README):
  - `ForwardedHeaders`: behind nginx, anonymous clients share the login rate limit, which is acceptable for a local demo;
  - a separate migrator;
  - production configuration.

**Verify A:**
1. `docker compose up --build -d`, wait until healthy.
2. `curl` http://localhost:8080 returns 200 (index), and login through `http://localhost:8080/api/v1/auth/login` returns
   200 with the cookie.
3. `GET /api/v1/doctors` through nginx returns 200.
4. In the browser pane: sign in at :8080, view the schedule, book a slot, cancel it; take a screenshot.
5. Jaeger shows `appointment-scheduler-api` traces.
6. Then stop `api` and `web` again, so local dev keeps its ports. Stop and ask: commit, or leave it in the working tree.

## Step B — Requirements catalogues + per-feature architecture
- **`docs/requirements/functional.md`**: business rules BR-1…BR-7 (from SPEC.md), then one section per feature:
  - availability (001), booking (002), manage schedule (003), auth and users (004, incl. audit), patient and doctor
    lookups (005 part);
  - the idempotent-write contract (009);
  - **planned, each marked as such**: patients CRUD (005), doctor schedules (006), clinics and time zones (007),
    notifications (008).

  Each requirement row carries the columns below. The specs stay the detailed acceptance criteria.

  | ID (FR-xxx-n) | Requirement in plain words | Roles | Status | API | Implemented in (files) | Verified by (unit test / live check) | Spec ACs |
  |---|---|---|---|---|---|---|---|
- **`docs/requirements/non-functional.md`**: NFR-1…NFR-11 from `000-nfr.md` expanded. Each has the requirement, the
  measurable target, why, how it is implemented (code and ADR references), how it is verified, and its status. Added NFRs
  for what is built but not listed:
  - the API contract (envelope, ADR-0015, and versioning, ADR-0017);
  - security hardening (headers, HSTS, CORS);
  - portability (Docker compose);
  - testability (unit + architecture only, plan 011).

  `000-nfr.md` stays the summary table and links to it.
- **`docs/architecture.md`** (overview, stale parts fixed): a containers diagram matching reality (nginx, API with
  Hangfire/Wolverine in process, SMTP and notifications marked planned), the Docker deployment topology, the layers, an
  `erDiagram` of the main tables, host composition and the request pipeline (kept), and an index of the feature files. The
  four existing sequence diagrams move into their feature files.
- **`docs/architecture/<feature>.md`**: each has a short purpose, the components involved (files), and Mermaid diagrams,
  usually a component flowchart plus a sequence diagram, with a state diagram where an entity has states:
  - `availability.md`: slot classification flowchart (Free / Booked / Past / PatientBusy), sequence;
  - `booking.md`: sequence with idempotency, pre-check, the exclusion constraint and the 409 race;
  - `manage-schedule.md`: cancel/reschedule sequences, an appointment `stateDiagram` (Scheduled → Cancelled), the `xmin`
    concurrency 409;
  - `auth-and-users.md`: login, refresh rotation and reuse (moved here), the lockout state, role/BR-7 authorization flow,
    user admin, audit;
  - `lookups.md`: doctors list, patient search; planned patients CRUD;
  - `idempotency.md`: filter decision flowchart (POST only, `[SkipIdempotency]`), replay sequence (moved here), record
    states, purge job;
  - `rate-limiting.md`: Polly pipeline flow, partition choice;
  - `errors-and-resilience.md`: exception → `ApiResponse` flowchart, the 503 database-outage path;
  - `background-jobs.md`: Hangfire recurring jobs (refresh-token and idempotency purges);
  - `messaging.md`: Wolverine store, RabbitMQ degraded mode/recovery `stateDiagram`, the planned notification flow (moved
    here, marked planned);
  - `observability.md`: traces and metrics to Jaeger/OTLP, the rate-limit counter;
  - `doctor-schedules.md`, `clinics-timezones.md`: marked Planned, with the design sketch from the spec.
- **Claude files**:
  - CLAUDE.md Definition of Done: "update `docs/requirements/*` and the feature's `docs/architecture/<feature>.md`";
  - the tech-writer agent and the `/new-feature` skill docs step;
  - README documentation map; CHANGELOG, PROMPTS.

**Verify B:**
- Every ```mermaid block in README.md, `docs/architecture.md` and `docs/architecture/*.md` renders without a parse error.
  Check this with a scratch HTML page that loads `mermaid@11` (jsdelivr), opened in the browser pane, which lists any block
  that fails.
- All relative links resolve (a small script checks that each `](path)` exists).
- Grep for hook corruption. Stop and ask: commit, or leave it in the working tree.
