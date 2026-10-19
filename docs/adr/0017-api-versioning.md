# ADR-0017 — API versioning with Asp.Versioning, version in the URL

- Status: Accepted · Date: 2026-10-19 · Backlog S-33 (versioning part)

## Context
The API was hard-coded to `/api/v1`. A breaking change, such as a new booking contract, needs a second version that runs
next to v1, and clients must be told which versions exist.

## Decision
- **Asp.Versioning.Mvc + ApiExplorer 8.1.1**, configured in `Api/Configuration/ApiVersioningConfig.cs`:
  - default 1.0, with `AssumeDefaultVersionWhenUnspecified`;
  - `ReportApiVersions`, which sends the `api-supported-versions` header;
  - readers combined: URL segment + `X-Api-Version` header + `api-version` query.
- **The URL is the source of truth**: routes are `api/v{version:apiVersion}/…` and every controller states
  `[ApiVersion(1.0)]`. The attribute is not inherited from `ApiControllerBase`; `ControllerConventionTests` enforce it.
  A header or query version must match the URL, otherwise the answer is 400 `UNSUPPORTED_API_VERSION`.
- **Unknown URL version** (`/api/v2/doctors`): Asp.Versioning answers 404, because that path does not exist. The problem-details
  writer turns it into 404 `UNSUPPORTED_API_VERSION` with the supported versions in `message`, instead of a generic `ROUTE_NOT_FOUND`.
- **OpenAPI**: the API explorer group `'v'VVV` = document `v1`; `SubstituteApiVersionInUrl` shows `/api/v1/...` in Scalar.
  A v2 adds a second document.
- **URLs are unchanged** (`/api/v1/...`), so the Angular app, the k6 plan and the tests are unaffected.
- **Refresh cookie**: its path stays `/api/v1/auth`. A v2 auth route needs its own cookie path, or a version-neutral one.

## Alternatives
- Header-only versioning: invisible in logs and links, and harder to cache.
- Query-string versioning: easy to drop by accident.
- A hand-rolled `/v1` prefix (what we had): no reporting and no side-by-side controllers.

## Consequences
+ v2 controllers can live next to v1 (`[ApiVersion(2.0)]`), and clients discover versions through a header.
− One more package pair. Every new controller must carry `[ApiVersion]` (the architecture test catches a missing one).
