---
name: new-feature
description: Single-session, spec-first checklist for adding a feature end to end (spec → tests → domain → application → infrastructure → api → web → docs) without dispatching role agents. Use for medium changes when the full /sdlc ceremony is not wanted.
---
# /new-feature — spec-first in one session

1. **Spec** — copy `docs/specs/_template.md` to `docs/specs/NNN-<name>.md`; write AC `AC-NNN-x` (Given/When/Then, incl. error
   codes). Show the AC list to the user before coding.
2. **Tests first** — one failing test per AC, named `AC_NNN_x_<Behavior>`:
   rules → `tests/AppointmentScheduler.UnitTests/<Feature>/`; HTTP/DB → `tests/AppointmentScheduler.IntegrationTests/<Feature>/`.
3. **Domain** — `src/AppointmentScheduler.Domain/<Feature>/`: entity methods/policies enforce rules; throw
   `BusinessRuleException(code, message)`.
4. **Application** — `src/AppointmentScheduler.Application/<Feature>/`: service method + request/response records; register in
   `Application/DependencyInjection.cs`.
5. **Infrastructure** — EF configuration in `Infrastructure/Persistence/Configurations/`; schema change → `/add-migration`.
6. **Api** — action on `Api/Controllers/<Feature>Controller.cs` (inherits `ApiControllerBase`); thin; correct policy, rate-limit and
   idempotent group.
7. **Web** — `web/src/app/core/api/` service + model, `web/src/app/features/<feature>/` component + spec.
8. **Verify** — `/verify`.
9. **Docs** — tick the spec Status, update `SPEC.md` (if API/data changed), `CHANGELOG.md`, `/log-prompt`.
