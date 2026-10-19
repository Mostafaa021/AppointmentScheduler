# ADR-0009 — Rate limiting with ASP.NET Core's built-in limiter

- Status: Accepted · Date: 2026-10-19

## Decision
`Microsoft.AspNetCore.RateLimiting` (no extra dependency), partitioned by user id (authenticated) or client IP:
| Policy | Algorithm | Limit | Applied to |
|---|---|---|---|
| `auth` | fixed window | 5 / min / IP | login, refresh |
| `writes` | token bucket | 30 / min / user, burst 10 | mutating groups |
| `reads` | sliding window (6 segments) | 300 / min / user | GET groups |
| global | concurrency | 200 concurrent requests | all |
Rejections → 429 `ApiResponse` error `RATE_LIMITED` (ADR-0015) with `Retry-After`; counted in metric `scheduler.rate_limit.rejected`.
Limits come from configuration so k6 tests and production can differ.

## Consequences
+ Protects login from brute force and the DB from bursts.
− In-memory per instance; with several replicas the effective limit multiplies → note to move to a Redis-backed limiter or gateway
  (e.g. YARP/Envoy) when scaling out.
