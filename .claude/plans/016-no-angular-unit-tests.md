# 016 — No Angular unit tests (for now)

- Status: **Done** · 2026-10-20 · user decision · applies to every later plan (with plan 011)
- Progress: specs + tooling removed ☑ · CI ☑ · Claude files ☑ · docs ☑ (commits only when the user says so)

## Context
The user doesn't want unit testing in the Angular app for now: "remove all … tests from angular projects, keep only
required". The required checks are the ones that keep the app working: the production build in CI and a live check of
the changed screens in the browser. The .NET unit and architecture tests are unaffected (plan 011).

## Rule for all coming plans
- Never add or plan Angular unit tests (`*.spec.ts`), Vitest/Karma/Jest tooling, or Playwright.
- The web app is verified by `cd web && npm run build` (CI does the same) plus a live browser check of the changed screens,
  recorded in the spec.
- To bring Angular tests back later, write a new plan: add `vitest` + `jsdom`, the `test` target (`@angular/build:unit-test`),
  `tsconfig.spec.json` and the `npm test` script again.

## Changes
- **Deleted** the 10 Angular spec files in `web/src/**` and `web/tsconfig.spec.json`.
- **Removed:**
  - the `test` architect target in `web/angular.json` and the `tsconfig.spec.json` reference in `web/tsconfig.json`;
  - the `test` script and the `vitest` + `jsdom` dev dependencies (`npm uninstall`, so the lock file follows);
  - the VS Code `ng test` launch and task entries;
  - the "Running unit tests" section in `web/README.md`.
  - `tsconfig.app.json` still excludes `*.spec.ts`, which is harmless.
- **CI:** the frontend job runs `npm ci` + `npm run build` only.
- **Claude files:**
  - CLAUDE.md (stack, commands, workflow step 2);
  - agents: frontend-developer, qa-engineer, architect, code-reviewer;
  - the `/verify` skill and `.claude/settings.json` (no `npm test`);
  - the plans index rule.
- **Docs:**
  - README (Tests), `docs/sdlc.md`;
  - the requirement catalogues: UI rows are now "live", and NFR-5 and NFR-17 are updated;
  - spec 004 (AC-004-21 is checked live in the browser), spec 009 test plan;
  - CHANGELOG, PROMPTS.

## Verification
- `cd web && npm run build` passes, and nothing under `web/src` imports test utilities.
- `dotnet test`: unchanged (72 unit, 16 architecture).
- Live: after sign-in, `localStorage` and `sessionStorage` are empty and `document.cookie` has no token (AC-004-21, which a
  Vitest spec used to cover).
