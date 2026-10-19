---
name: verify
description: Run every quality check for the Appointment Scheduler (build, unit, architecture and integration tests, Angular tests and build, compose health) and report evidence. Use before claiming work is done, before commits, and as the demo step of the SDLC.
---
# /verify

Run in order, stop at the first failure and report it with output:

1. Infra up: `docker compose up -d postgres rabbitmq jaeger` (Docker Desktop must be running).
2. Backend: `dotnet build -warnaserror` then `dotnet test` (unit + architecture + Testcontainers integration).
3. Frontend: `cd web && npm ci` (only if `node_modules` missing) → `npm test -- --watch=false` → `npm run build`.
4. Full stack (M6+): `docker compose up -d --build` → `docker compose ps` all healthy → `curl -fsS http://localhost:8080/health/ready`.
5. Smoke (optional): `/load-test smoke`.

Report as a table: check · result · evidence (test counts, build time, failing test names). Never summarize a failure as success.
