---
name: load-test
description: Run a k6 scenario from perf/k6 against the running stack and compare results with the NFR thresholds in docs/specs/000-nfr.md. Use for "load test", "k6", "performance", or verifying NFR-1/2/3/5/6.
---
# /load-test [script]

Scripts (`perf/k6/`): `smoke`, `load-availability`, `stress-booking`, `race-same-slot`, `idempotency-replay`, `rate-limit`, `soak`.

1. Stack must be up: `docker compose up -d --build` and `curl -fsS http://localhost:8080/health/ready`.
2. Run (default `smoke`):
   ```bash
   docker compose --profile perf run --rm k6 run /scripts/<script>.js
   ```
   Against a locally running API instead: `k6 run -e BASE_URL=http://localhost:5080 perf/k6/<script>.js` (if k6 is installed).
3. Report: thresholds passed/failed, p95/p99 latency, error rate, and for `race-same-slot` the count of 201 vs 409 (must be 1 vs N-1).
4. Compare with `docs/specs/000-nfr.md`; on failure, open Jaeger (http://localhost:16686) for the slowest traces and name the
   bottleneck before proposing a fix.
5. Results JSON goes to `perf/k6/results/` (git-ignored). Note the summary in the relevant spec.
