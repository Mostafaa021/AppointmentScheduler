namespace AppointmentScheduler.Infrastructure.Idempotency;

/// <summary>Configuration section <c>Idempotency</c> (spec 009).</summary>
public sealed class IdempotencyOptions
{
    public const string Section = "Idempotency";

    /// <summary>How long a key is remembered (AC-009-7: 24 h); the hourly purge job deletes older records.</summary>
    public int RecordTtlHours { get; set; } = 24;

    /// <summary>
    /// An <c>InProgress</c> record older than this may be taken over by the same request, so a crash between handler and
    /// completion doesn't block the key until it expires.
    /// </summary>
    public int InProgressTimeoutSeconds { get; set; } = 60;
}
