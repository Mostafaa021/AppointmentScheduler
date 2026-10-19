# ADR-0008 — Resilience patterns: Polly v8 for every one of them

- Status: Accepted · Date: 2026-10-19 · Revised 2026-10-19: **Polly is the single resilience library** (user decision, plan 009).
  That covers rate limiting (ADR-0009) and database resilience, both implemented as named Polly pipelines.

## Decision
All resilience is built from Polly v8 `ResiliencePipeline`s registered by name with `AddResiliencePipeline(…)`
(`Polly.Extensions`) and resolved from `ResiliencePipelineProvider<string>`. One library, one vocabulary (timeout / retry /
circuit breaker / rate limiter), one place to tune.

| Dependency | Pattern | Implementation |
|---|---|---|
| Inbound HTTP | Concurrency cap + per-user/IP rate limits | Polly pipelines `rate-limit:*` with `AddConcurrencyLimiter` / `AddRateLimiter` over partitioned limiters (ADR-0009, S-32 — **done**) |
| PostgreSQL | Timeout → retry (exponential + jitter, transient Npgsql errors only) → circuit breaker | Polly pipeline `database`, used by EF Core through a custom execution strategy; explicit transactions wrapped in `CreateExecutionStrategy().ExecuteAsync` (S-33) |
| SMTP (background jobs in the Api host) | Timeout → retry (exponential + jitter) → circuit breaker | Polly pipeline `smtp`: timeout 10 s, 3 retries, breaker opens at 50 % failures over 30 s (min 5) for 30 s (M5) |
| Outbound HTTP (future integrations) | Standard handler | `AddStandardResilienceHandler()` (Polly-based) on typed `HttpClient`s |
| Message handling | Retry → DLQ | Wolverine error policies (ADR-0007); Wolverine owns its own retries |
| Browser → API | Retry GET + keyed writes | Angular interceptor: 3 tries, backoff 500 ms·2ⁿ, honours `Retry-After`, only on 0/5xx/429 |

Never retry non-idempotent writes without an idempotency key (spec 009); never retry 4xx except 429.

## Consequences
+ Transient failures self-heal. A dead dependency fails fast behind a breaker instead of tying up threads.
+ Every pattern is configured and observed the same way (Polly telemetry flows through OpenTelemetry).
− Retries amplify load, which bounded counts, jitter and breakers contain.
− ASP.NET Core's built-in rate-limiting middleware is not used, so its `[EnableRateLimiting]` attributes don't apply; ours is
  `[RateLimitPolicy]`.
