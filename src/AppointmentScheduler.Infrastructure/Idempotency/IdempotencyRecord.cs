namespace AppointmentScheduler.Infrastructure.Idempotency;

public enum IdempotencyStatus { InProgress, Completed }

/// <summary>
/// One <c>(user, Idempotency-Key)</c> pair (spec 009). Written only through set-based SQL by <see cref="EfIdempotencyStore"/>,
/// never tracked, so it can't interfere with the handler's own unit of work.
/// </summary>
public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Key { get; set; } = "";
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";

    /// <summary>SHA-256 (hex) of method + path + body; a different hash under the same key is a reuse (422).</summary>
    public string RequestHash { get; set; } = "";

    public IdempotencyStatus Status { get; set; }
    public int? ResponseStatusCode { get; set; }

    /// <summary>The response body exactly as sent (text, not jsonb, so replay is byte-for-byte).</summary>
    public string? ResponseBody { get; set; }

    /// <summary>Kept headers (<c>Location</c>, <c>ETag</c>, <c>Content-Type</c>) as a JSON object.</summary>
    public string? ResponseHeaders { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
