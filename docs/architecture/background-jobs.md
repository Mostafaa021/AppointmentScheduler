# Background jobs ✅

Recurring housekeeping runs inside the API process with Hangfire, so there is no separate worker to deploy.
[ADR-0014](../adr/0014-hangfire-background-jobs.md).

## Components
| Piece | Job |
|---|---|
| `Infrastructure/BackgroundJobs/BackgroundJobsSetup.cs` | `AddBackgroundJobs` (Hangfire + PostgreSQL storage, schema `hangfire`); `UseBackgroundJobs` registers the recurring jobs and the Development-only dashboard |
| `Infrastructure/Auth/PurgeExpiredRefreshTokensJob.cs` | deletes refresh tokens past expiry (hourly) |
| `Infrastructure/Idempotency/PurgeExpiredIdempotencyRecordsJob.cs` | deletes idempotency records past their 24 h time-to-live (hourly) |
| `BackgroundJobs:Enabled` | `false` turns off the server and the recurring registrations |

## How a recurring job runs
```mermaid
sequenceDiagram
  participant P as Program.cs
  participant RJ as IRecurringJobManager
  participant HS as Hangfire server (in the api process)
  participant DB as PostgreSQL (schema hangfire)
  participant J as PurgeExpired…Job
  P->>RJ: UseBackgroundJobs → AddOrUpdate(job, Cron.Hourly)
  RJ->>DB: upsert recurring definition
  loop every hour
    HS->>DB: fetch due jobs (distributed lock — one instance runs it)
    HS->>J: RunAsync(ct) in a DI scope
    J->>DB: DELETE … WHERE expires_at_utc < now
    HS->>DB: mark succeeded (or retry with backoff on failure)
  end
```

## Where things run
```mermaid
flowchart LR
  subgraph api[api process]
    MVC[HTTP requests]
    HS[Hangfire server]
    WV[Wolverine handlers]
  end
  HS --> DB[(PostgreSQL<br/>hangfire schema)]
  HS -.->|planned, spec 008| SMTP[send e-mail jobs]
  DEV[/hangfire dashboard<br/>Development, local requests only/] --> HS
```
In Docker the dashboard isn't reachable, because it accepts local requests only. Use local development to see it.
