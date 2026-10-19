# Backlog

Owned by the `scrum-master` agent. One row per story; status mirrors the spec's Status table.
Statuses: `Todo → Refined → Designed → Built → Tested → Reviewed → Released → Documented (Done)`.

## Sprint 0 — M0 Foundation
| ID | Story | Spec | Status |
|---|---|---|---|
| S-01 | Repo, solution, central packages, compose infra | — | Done |
| S-02 | Specs 000–009, ADRs 0001–0010, CLAUDE.md, SDLC agents & skills | all | Done |
| S-03 | CI skeleton + ArchitectureTests | 000 | Done |

## Sprint 1 — M1 Challenge core (tag `v1-challenge`)
| ID | Story | Spec | Status |
|---|---|---|---|
| S-10 | Domain: Appointment, DaySchedule, BR-1..5 (TDD) | 001, 002, 003 | Done |
| S-11 | EF model, exclusion constraints migration, seed | 002 | Done |
| S-12 | Availability + doctors + patients search endpoints | 001, 005 | Done |
| S-13 | Book / cancel / reschedule endpoints + race test | 002, 003 | Done |
| S-14 | Angular schedule page, book & reschedule dialogs | 001–003 | Done |
| S-15 | README, SPEC.md, PROMPTS.md for submission | — | Done |

## Sprint 2 — M2 Security (active)
Opened 2026-10-19 by scrum-master. Next: product-owner re-confirms DoR → architect → human approval.

| ID | Story | Spec | Status |
|---|---|---|---|
| S-20 | Identity + JWT + refresh rotation, user admin, demo users (AC-004-1..7, 10..14, 26) | 004 | Refined |
| S-21 | Role policies + Doctor resource check (BR-7) (AC-004-8, 9; AC-003-9) | 004, 003 | Refined |
| S-22 | Angular login, guards, auth interceptor (AC-004-15..21) | 004 | Refined |
| S-23 | FluentValidation, security headers, audit log (AC-004-22..25) | 004, 000 | Refined |

## Sprint 3 — M3 Reliability
| ID | Story | Spec | Status |
|---|---|---|---|
| S-30 | Idempotency filter + records table + purge job + Angular key interceptor | 009 | Refined |
| S-31 | ETag / If-Match on appointments | 003 | Refined |
| S-32 | Rate limiting policies | 000 | Refined |
| S-33 | API versioning, output caching, Npgsql retry | 000 | Todo |

## Sprint 4 — M4 Domain expansion
| ID | Story | Spec | Status |
|---|---|---|---|
| S-40 | Clinics + per-clinic time zone + holidays | 007 | Refined |
| S-41 | Working hours / breaks / time off / appointment types | 006 | Refined |
| S-42 | Patient CRUD | 005 | Refined |
| S-43 | Admin UI | 005–007 | Todo |

## Sprint 5 — M5 Messaging
| ID | Story | Spec | Status |
|---|---|---|---|
| S-50 | Domain events → Wolverine outbox → RabbitMQ → in-process handlers | 008 | Refined |
| S-51 | E-mails + reminders + SMTP resilience pipeline + DLQ | 008 | Refined |

## Sprint 6 — M6 Observability & ops
| ID | Story | Spec | Status |
|---|---|---|---|
| S-60 | OTel → Jaeger, Serilog, /metrics, health checks | 000 | Refined |
| S-61 | Dockerfiles, full compose, migrator, operations.md | 000 | Todo |

## Sprint 7 — M7 Performance & hardening
| ID | Story | Spec | Status |
|---|---|---|---|
| S-70 | k6 suite with thresholds | 000 | Refined |
| S-71 | Playwright e2e, CI green, security + code review passes | 000 | Todo |
