# ADR-0009 — Rate limiting with Polly pipelines

- Status: Accepted · Date: 2026-10-19 · Revised 2026-10-19: built on **Polly v8** instead of
  `Microsoft.AspNetCore.RateLimiting` (user decision: one resilience library, ADR-0008). Implemented in S-32.

## Decision
Named Polly pipelines (`Api/Configuration/RateLimitingConfig.cs`), run by `Api/RateLimiting/PollyRateLimitingMiddleware` right
after authentication. Every request goes through `global` and then its policy. The policy is `[RateLimitPolicy]` on the action,
else `reads` for GET/HEAD and `writes` for the rest. Partitions use the user id (`sub`), else the client IP; `auth` always uses
the IP.

| Pipeline | Polly strategy → limiter | Limit (config `RateLimiting`) | Applied to |
|---|---|---|---|
| `rate-limit:global` | `AddConcurrencyLimiter` | 200 concurrent, no queue | every request |
| `rate-limit:reads` | `AddRateLimiter` → partitioned sliding window (6 segments) | 300 / min / user | GET/HEAD |
| `rate-limit:writes` | `AddRateLimiter` → partitioned token bucket | burst 10, refilled 1 per 2 s (30 / min) / user | other methods |
| `rate-limit:auth` | `AddRateLimiter` → partitioned fixed window | 5 / min / IP | login, refresh |

- **Rejections:** `RateLimiterRejectedException` → 429 `ApiResponse` error `RATE_LIMITED` (`ApiErrorCodes`) with
  `Retry-After` from the limiter's lease metadata. They are counted in `scheduler.rate_limit.rejected` (tag `policy`, meter
  `AppointmentScheduler`).
- **Exempt:** `/health` is never limited.
- **Config:** limits come from configuration, so tests, k6 runs and production can differ. `RateLimiting:Enabled=false` removes
  the middleware.
- **Why Polly here:** the same pipeline model as the database and SMTP resilience, so one library, one telemetry and one way to
  tune everything (ADR-0008). Partitioned limiters (`System.Threading.RateLimiting`) keep per-user state bounded inside each
  pipeline; Polly's context carries the partition key.

## Consequences
+ Protects login from brute force (with the account lockout) and the database from bursts; one busy user can't starve others.
− In-memory per instance: with several replicas the effective limit multiplies. Move to a gateway (YARP/Envoy) or a
  distributed limiter when scaling out.
− Behind a reverse proxy `RemoteIpAddress` is the proxy: add `ForwardedHeaders` with the container setup (S-61), or anonymous
  clients share one partition.
