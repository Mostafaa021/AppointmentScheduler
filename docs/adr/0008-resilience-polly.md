# ADR-0008 — Resilience: Polly for other services and rate limits; the database relies on atomicity

- Status: Accepted · Date: 2026-10-19 · Revised 2026-10-20 (user decision, plan 009): **Polly is used only when we talk to
  another service** (SMTP, outbound HTTP), plus the inbound rate limits (ADR-0009). **The database gets no Polly pipeline and
  no retry**: consistency comes from the atomic unit of work, and a database outage is reported, not retried.

## Decision
Resilience toward other services is built from Polly v8 `ResiliencePipeline`s registered by name with
`AddResiliencePipeline(…)` (`Polly.Extensions`) and resolved from `ResiliencePipelineProvider<string>`.

The database is ours and transactional, so it is handled differently:
- each use case stages changes through repositories and commits once with `IUnitOfWork.SaveChangesAsync` (ADR-0016), and the
  four multi-step Identity operations use explicit transactions, so a failure leaves nothing half-saved;
- there is no EF execution strategy (`EnableRetryOnFailure`) and no Polly `database` pipeline;
- a transient failure (`DbException.IsTransient`: broken connection, timeout) becomes 503 `SERVICE_UNAVAILABLE` with
  `Retry-After: 5` in `GlobalExceptionHandler`. Anything else from the database stays a generic 500;
- the client retries. Writes are safe to retry because the idempotency filter releases the key on any 5xx (spec 009).

| Dependency | Pattern | Implementation |
|---|---|---|
| Inbound HTTP | Concurrency cap + per-user/IP rate limits | Polly pipelines `rate-limit:*` with `AddConcurrencyLimiter` / `AddRateLimiter` over partitioned limiters (ADR-0009, S-32 — **done**) |
| PostgreSQL | **No retry**: atomic unit of work / transactions; transient failure → 503 + `Retry-After` | `GlobalExceptionHandler` (S-33 — **done**) |
| SMTP (background jobs in the Api host) | Timeout → retry (exponential + jitter) → circuit breaker | Polly pipeline `smtp`: timeout 10 s, 3 retries, breaker opens at 50 % failures over 30 s (min 5) for 30 s (M5) |
| Outbound HTTP (future integrations) | Standard handler | `AddStandardResilienceHandler()` (Polly-based) on typed `HttpClient`s |
| Message handling | Retry → DLQ | Wolverine error policies (ADR-0007); Wolverine owns its own retries |
| Browser → API | Retry GET + keyed writes | Angular interceptor: 3 tries, backoff 500 ms·2ⁿ, honours `Retry-After`, only on 0/5xx/429 |

Never retry non-idempotent writes without an idempotency key (spec 009); never retry 4xx except 429.

## Consequences
+ Other services that fail transiently self-heal. A dead one fails fast behind a breaker instead of tying up threads.
+ Database code stays plain EF Core: no execution-strategy wrapping around transactions, no retry state inside a request.
+ A database outage is visible and honest: 503 with `Retry-After`, never a half-saved change.
− A blip the server could have absorbed reaches the client, which must retry (the Angular interceptor does).
− If a commit succeeded but its acknowledgement was lost, the client's retry repeats the use case. Idempotency replays it when
  the key was stored, otherwise the exclusion constraint answers 409.
− ASP.NET Core's built-in rate-limiting middleware is not used, so its `[EnableRateLimiting]` attributes don't apply; ours is
  `[RateLimitPolicy]`.
