# Rate limiting ✅

Brute-force logins and one busy client must not starve everyone else. NFR-6 in
[non-functional.md](../requirements/non-functional.md#nfr-6--abuse-protection-rate-limits) ·
[ADR-0009](../adr/0009-rate-limiting.md) · [ADR-0008](../adr/0008-resilience-polly.md) (Polly).

## Components
| Piece | Job |
|---|---|
| `Api/Configuration/RateLimitingConfig.cs` | registers the named Polly v8 pipelines from section `RateLimiting`; `UseRateLimitingConfiguration()` adds the middleware when `Enabled` |
| `Api/RateLimiting/PollyRateLimitingMiddleware.cs` | runs after authentication: global pipeline, then the endpoint's policy pipeline |
| `RateLimitPolicies` / `[RateLimitPolicy(...)]` | pipeline names; an action opts into a non-default policy (login and refresh use `auth`) |
| `RateLimitPartitionKey` | who a request counts against: `user:{sub}`, else `ip:{address}`; `auth` is always per IP |
| `RateLimitMetrics` | counter `scheduler.rate_limit.rejected` (tag `policy`) |

## Request flow
```mermaid
flowchart TD
  R[request, after authentication] --> H{path starts with /health?}
  H -- yes --> N[next — never limited]
  H -- no --> G[rate-limit:global<br/>concurrency limiter, 200 in flight, no queue]
  G --> P{policy}
  P -- "[RateLimitPolicy(Auth)]" --> A[rate-limit:auth<br/>fixed window 5 / 60 s per IP]
  P -- GET / HEAD --> RD[rate-limit:reads<br/>sliding window 300 / 60 s, 6 segments]
  P -- other methods --> W[rate-limit:writes<br/>token bucket: burst 10, +1 every 2 s]
  A & RD & W --> OK{permit?}
  OK -- yes --> NEXT[next: authorization → endpoint]
  OK -- no --> REJ[429 RATE_LIMITED + Retry-After<br/>counter scheduler.rate_limit.rejected]
```

## Partitions
```mermaid
flowchart LR
  Q[request] --> PA{policy is auth?}
  PA -- yes --> IP[ip:RemoteIpAddress]
  PA -- no --> U{signed in<br/>has 'sub'?}
  U -- yes --> USR["user:{sub}"]
  U -- no --> IP
```
Each policy keeps a partitioned limiter (`System.Threading.RateLimiting`), so one user's burst never spends another user's
budget. The partition key travels to the limiter in Polly's `ResilienceContext`.

**Limits.** Counters live in memory per API instance; with several replicas, move the limits to a gateway. Behind a reverse
proxy (the Docker nginx), `RemoteIpAddress` is the proxy's address until `ForwardedHeaders` is added (S-61), so anonymous
clients share one login budget.
