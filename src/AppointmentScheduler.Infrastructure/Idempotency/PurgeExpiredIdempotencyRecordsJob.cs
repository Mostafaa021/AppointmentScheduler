using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AppointmentScheduler.Infrastructure.Idempotency;

/// <summary>Hourly Hangfire job (AC-009-7): deletes idempotency records past their 24 h lifetime.</summary>
public sealed partial class PurgeExpiredIdempotencyRecordsJob(
    AppDbContext db,
    TimeProvider clock,
    ILogger<PurgeExpiredIdempotencyRecordsJob> logger)
{
    public const string RecurringJobId = "purge-expired-idempotency-records";

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var deleted = await db.IdempotencyRecords.Where(r => r.ExpiresAtUtc <= now).ExecuteDeleteAsync(ct);
        LogPurged(logger, deleted, now);
        return deleted;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} idempotency records expired before {Now}")]
    private static partial void LogPurged(ILogger logger, int count, DateTimeOffset now);
}
