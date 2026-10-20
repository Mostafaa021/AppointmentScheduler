# 017 — pgAdmin web UI in Docker Compose

- Status: **Done** · 2026-10-20 · user choice ("pgAdmin in Docker") · S-61 (tooling)

## Context
The user's desktop pgAdmin (9.18 on Windows) crashed with `exception: access violation writing 0x0000000000000000` on
connect. This is a known bug, [pgadmin4#10428](https://github.com/pgadmin-org/pgadmin4/issues/10428): GSSAPI-enabled libpq
crashes unless `gssencmode=disable` is set. Claude can't drive desktop apps, so the user chose a browser-based pgAdmin
in Docker, pre-connected to the scheduler database.

## Changes
- **`docker-compose.yml`**: service `pgadmin` (`dpage/pgadmin4:9`, profile `tools`, so it is not started by
  `docker compose up`):
  - desktop mode (`SERVER_MODE=False`, no master password), so there is no login;
  - `127.0.0.1:${PGADMIN_PORT:-5050}:80`, so it is not reachable from the LAN;
  - an entrypoint wrapper writes `/tmp/pgpass` (0600) from `POSTGRES_USER` / `POSTGRES_PASSWORD`, then runs the image's
    `/entrypoint.sh`;
  - volume `pgadmin-data` keeps settings and saved queries.
- **`tools/pgadmin/servers.json`**: server "AppointmentScheduler" (host `postgres`, db and user `scheduler`,
  `gssencmode=disable`, `passfile=/tmp/pgpass`), loaded on first start.
- **Docs:**
  - README: URL row, a Docker-first pgAdmin section, and the desktop table with the `gssencmode` fix;
  - CLAUDE.md commands, `.env.example` (`PGADMIN_PORT`), the architecture deployment note, CHANGELOG.

## Verification
- The container log shows "Added 1 Server(s)", and `/tmp/pgpass` is `-rw-------`.
- In the browser at http://localhost:5050: no login, **AppointmentScheduler** connects without a password prompt.
- Query Tool on `scheduler`: a per-doctor appointment count → "Successfully run … 4 rows".
