# ADR-0008 — Resilience patterns

- Status: Accepted · Date: 2026-10-19

## Decision
| Dependency | Pattern | Implementation |
|---|---|---|
| PostgreSQL | Retry transient errors | Npgsql `EnableRetryOnFailure(3)`; explicit transactions wrapped in `CreateExecutionStrategy().ExecuteAsync` |
| SMTP (Worker) | Timeout → retry (exponential + jitter) → circuit breaker | Polly v8 `ResiliencePipeline` registered via `Microsoft.Extensions.Resilience` (`AddResiliencePipeline("smtp")`): timeout 10 s, 3 retries, breaker opens at 50 % failures over 30 s (min 5) for 30 s |
| Outbound HTTP (future integrations) | Standard handler | `AddStandardResilienceHandler()` on typed `HttpClient`s |
| Message handling | Retry → DLQ | Wolverine error policies (ADR-0007) |
| Browser → API | Retry GET + keyed writes | Angular interceptor: 3 tries, backoff 500 ms·2ⁿ, honors `Retry-After`, only on 0/5xx/429 |

Never retry non-idempotent writes without an idempotency key; never retry 4xx except 429.

## Consequences
+ Transient failures self-heal; a dead SMTP server cannot exhaust Worker threads.
− Retries amplify load → bounded counts + jitter + breaker.
