using System.Text.Json;
using AppointmentScheduler.Application.Idempotency;
using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppointmentScheduler.Infrastructure.Idempotency;

/// <summary>
/// ADR-0006 on PostgreSQL. <c>INSERT … ON CONFLICT (user_id, key) DO NOTHING</c> claims the key atomically; everything else
/// is set-based (<c>ExecuteUpdate</c>/<c>ExecuteDelete</c>) or <c>AsNoTracking</c>, so nothing joins the handler's unit of work.
/// </summary>
internal sealed class EfIdempotencyStore(AppDbContext db, TimeProvider clock, IOptions<IdempotencyOptions> options) : IIdempotencyStore
{
    private const string InProgress = nameof(IdempotencyStatus.InProgress);

    public async Task<IdempotencyBegin> TryBeginAsync(IdempotencyRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (await TryInsertAsync(request, now, ct) is { } started) return IdempotencyBegin.Started(started);

        var existing = await db.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == request.UserId && r.Key == request.Key, ct);

        if (existing is null) // purged between our INSERT and SELECT: claim it now
            return await TryInsertAsync(request, now, ct) is { } id ? IdempotencyBegin.Started(id) : IdempotencyBegin.InProgress;

        if (existing.ExpiresAtUtc <= now) // expired but not yet purged: the key is free again
        {
            await db.IdempotencyRecords.Where(r => r.Id == existing.Id).ExecuteDeleteAsync(ct);
            return await TryInsertAsync(request, now, ct) is { } id ? IdempotencyBegin.Started(id) : IdempotencyBegin.InProgress;
        }

        if (existing.RequestHash != request.RequestHash) return IdempotencyBegin.KeyReused;

        if (existing.Status == IdempotencyStatus.Completed)
        {
            var headers = existing.ResponseHeaders is null
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(existing.ResponseHeaders) ?? [];
            return IdempotencyBegin.Replay(new StoredResponse(existing.ResponseStatusCode ?? 200, existing.ResponseBody ?? "", headers));
        }

        // Still in progress. Take over only if it is older than the timeout (the first attempt crashed), and only if nobody
        // else took it over first: the WHERE on the old created_at makes that a compare-and-swap.
        var staleBefore = now.AddSeconds(-options.Value.InProgressTimeoutSeconds);
        if (existing.CreatedAtUtc < staleBefore)
        {
            var claimed = await db.IdempotencyRecords
                .Where(r => r.Id == existing.Id && r.Status == IdempotencyStatus.InProgress && r.CreatedAtUtc == existing.CreatedAtUtc)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAtUtc, now), ct);
            if (claimed == 1) return IdempotencyBegin.Started(existing.Id);
        }

        return IdempotencyBegin.InProgress;
    }

    public Task CompleteAsync(Guid recordId, StoredResponse response, CancellationToken ct)
    {
        var headers = JsonSerializer.Serialize(response.Headers);
        return db.IdempotencyRecords.Where(r => r.Id == recordId).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Status, IdempotencyStatus.Completed)
            .SetProperty(r => r.ResponseStatusCode, response.StatusCode)
            .SetProperty(r => r.ResponseBody, response.Body)
            .SetProperty(r => r.ResponseHeaders, headers), ct);
    }

    public Task ReleaseAsync(Guid recordId, CancellationToken ct) =>
        db.IdempotencyRecords.Where(r => r.Id == recordId).ExecuteDeleteAsync(ct);

    /// <summary>The record id when this call claimed the key; null when another record already holds it.</summary>
    private async Task<Guid?> TryInsertAsync(IdempotencyRequest request, DateTimeOffset now, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(now);
        var expires = now.AddHours(options.Value.RecordTtlHours);
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO idempotency_records (id, user_id, key, method, path, request_hash, status, created_at_utc, expires_at_utc)
            VALUES ({id}, {request.UserId}, {request.Key}, {request.Method}, {request.Path}, {request.RequestHash},
                    {InProgress}, {now}, {expires})
            ON CONFLICT (user_id, key) DO NOTHING
            """, ct);
        return inserted == 1 ? id : null;
    }
}
