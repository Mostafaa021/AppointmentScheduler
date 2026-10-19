# ADR-0001 — Clean Architecture with feature folders, no mediator

- Status: Accepted · Date: 2026-10-19 · Data-access part superseded by [ADR-0016](0016-repository-unit-of-work.md)
  (repositories + unit of work replace `IAppDbContext`; Application no longer references EF Core)
- Amended 2026-10-19: the Api references Application and Infrastructure only, never Domain (architecture test). Expected
  failures reach it as `ApplicationError`, and DTOs expose Application enums.

## Context
The app must be easy to extend (new features, a live change in the interview) and simulate production structure, but a 2-day
core must stay readable.

## Decision
Four projects: `Domain` (entities, rules, events — no dependencies), `Application` (use-case services, DTOs, validators,
`IAppDbContext` port), `Infrastructure` (EF Core, Identity, messaging, SMTP), `Api` (the single host/composition root).
Inside each, **feature folders** (`Appointments/`, `Patients/` …). Application talks to EF through `IAppDbContext` exposing
`DbSet`s — no generic repository layer. No MediatR/AutoMapper: plain service classes and hand-written mapping.
Layer rules are enforced by `ArchitectureTests` (NetArchTest).

## Consequences
+ A feature is one folder per layer; dependency direction is testable.
+ Fewer abstractions to explain; EF LINQ stays composable.
− Application references `Microsoft.EntityFrameworkCore` (for `DbSet`/async LINQ). Accepted pragmatism; swapping ORMs is not a goal.
