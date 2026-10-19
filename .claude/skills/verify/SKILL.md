---
name: verify
description: Run every quality check for the Appointment Scheduler (build, .NET unit and architecture tests, Angular build, compose health) and report evidence. Use before claiming work is done, before commits, and as the demo step of the SDLC.
---
# /verify

Run in order, stop at the first failure and report it with output:

1. Backend: `dotnet build -warnaserror` then `dotnet test` (unit + architecture; no Docker needed).
2. Frontend: `cd web && npm ci` (only if `node_modules` missing) → `npm run build` (no Angular unit tests, plan 016; check the
   changed screens in the browser instead).
3. Live check when the change is observable over HTTP: `docker compose up -d postgres rabbitmq jaeger`, run the API, and
   exercise the changed endpoints with `curl` (status, `errorCode`, headers). This replaces the removed integration tests.
4. Full stack (M6+): `docker compose up -d --build` → `docker compose ps` all healthy → `curl -fsS http://localhost:8080/health/ready`.

Report as a table: check · result · evidence (test counts, build time, failing test names, curl output). Never summarize a
failure as success.
