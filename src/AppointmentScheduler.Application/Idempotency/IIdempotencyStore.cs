namespace AppointmentScheduler.Application.Idempotency;

/// <summary>
/// Persistence port for idempotent writes (spec 009, ADR-0006). The Api's idempotency filter drives it; Infrastructure stores
/// the records in PostgreSQL, where the unique <c>(user_id, key)</c> index is the lock — no distributed lock needed.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Claims <paramref name="request"/>'s key for this user, or reports why it can't: the key was already completed (replay),
    /// is still running, or was used for a different request.
    /// </summary>
    Task<IdempotencyBegin> TryBeginAsync(IdempotencyRequest request, CancellationToken ct);

    /// <summary>Stores the handler's response and marks the record completed, so retries replay it (AC-009-2, AC-009-3).</summary>
    Task CompleteAsync(Guid recordId, StoredResponse response, CancellationToken ct);

    /// <summary>Forgets the key so the client may retry, after a 5xx or an aborted request (AC-009-6).</summary>
    Task ReleaseAsync(Guid recordId, CancellationToken ct);
}

/// <summary>One write request, identified by its caller and key and fingerprinted by method + path + body.</summary>
public sealed record IdempotencyRequest(Guid UserId, string Key, string Method, string Path, string RequestHash);

/// <summary>What the handler answered, kept byte-for-byte for replay. Header names are those the filter chose to keep.</summary>
public sealed record StoredResponse(int StatusCode, string Body, IReadOnlyDictionary<string, string> Headers);

public enum IdempotencyOutcome
{
    /// <summary>The key is ours now; run the handler, then complete or release <see cref="IdempotencyBegin.RecordId"/>.</summary>
    Started,

    /// <summary>Same key + same request already completed; answer with <see cref="IdempotencyBegin.Response"/>.</summary>
    Replay,

    /// <summary>Same key + same request is still being handled (409 + Retry-After).</summary>
    InProgress,

    /// <summary>Same key, different method/path/body (422).</summary>
    KeyReused,
}

public sealed record IdempotencyBegin(IdempotencyOutcome Outcome, Guid? RecordId = null, StoredResponse? Response = null)
{
    public static IdempotencyBegin Started(Guid recordId) => new(IdempotencyOutcome.Started, recordId);

    public static IdempotencyBegin Replay(StoredResponse response) => new(IdempotencyOutcome.Replay, Response: response);

    public static readonly IdempotencyBegin InProgress = new(IdempotencyOutcome.InProgress);

    public static readonly IdempotencyBegin KeyReused = new(IdempotencyOutcome.KeyReused);
}
