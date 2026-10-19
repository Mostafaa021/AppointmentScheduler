# 006 — Host config as C# 14 extension blocks + API versioning, observability, messaging

- Status: **Done** · 2026-10-19 · Backlog S-33 (versioning), S-50 (wiring), S-60 (traces) · ADR-0007, 0010, 0017
- Backfilled from git history and PROMPTS P-17.

## Context
The user wanted `*Config` classes with `extension(WebApplicationBuilder builder)` blocks, like their other service, and chose
to build API versioning, observability and messaging now. Built-in OpenAPI + Scalar stays (no Swashbuckle); FluentValidation
stays in Application.

## Decisions
- `ApiConfig` / `SecurityConfig` / `OpenApiConfig` / `ApiVersioningConfig` / `ObservabilityConfig` / `MessagingConfig`.
- `AddProblemDetails` is plumbing only; `ApiResponseProblemDetailsWriter` + `ApiResponseClientErrorFactory` write every
  framework error as `ApiResponse`.
- Versioning: the URL segment is the source of truth; an unknown URL version → 404 `UNSUPPORTED_API_VERSION`.
- OTel: traces → Jaeger over OTLP, metrics only with an endpoint.
- Wolverine: PostgreSQL store, RabbitMQ when the startup probe succeeds, otherwise degraded mode + recovery service.
- Fixes found on the way:
  - CA1708 false positive on extension blocks;
  - empty 415 body;
  - Wolverine needs `RuntimeCompilation`;
  - test host crash (dispose the host before the database).

## Delivered
Commits `1898674`, `655d68a`, `afe2110`, `1888c70`, `7a3da12`, `5f15839`.
