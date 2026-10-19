using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AppointmentScheduler.Infrastructure.Auth;

/// <summary>
/// Hourly Hangfire job: deletes refresh tokens that expired more than a day ago. They can no longer be used (refresh with an
/// expired token is rejected) and reuse detection (AC-004-6) only matters within a token's lifetime, so nothing is lost.
/// </summary>
public sealed partial class PurgeExpiredRefreshTokensJob(AppDbContext db, TimeProvider clock, ILogger<PurgeExpiredRefreshTokensJob> logger)
{
    public const string RecurringJobId = "purge-expired-refresh-tokens";
    public static readonly TimeSpan Grace = TimeSpan.FromDays(1);

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow() - Grace;
        var deleted = await db.RefreshTokens.Where(t => t.ExpiresAtUtc < cutoff).ExecuteDeleteAsync(ct);
        LogPurged(logger, deleted, cutoff);
        return deleted;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} refresh tokens expired before {Cutoff}")]
    private static partial void LogPurged(ILogger logger, int count, DateTimeOffset cutoff);
}
