namespace AppointmentScheduler.Api.Idempotency;

/// <summary>The HTTP headers of the idempotency contract (spec 009).</summary>
public static class IdempotencyHeaders
{
    /// <summary>Request header every write carries: 1–100 characters, a UUID per user action.</summary>
    public const string Key = "Idempotency-Key";

    /// <summary>Response header on a replayed response (value <c>true</c>).</summary>
    public const string Replayed = "Idempotent-Replayed";

    public const int MaxKeyLength = 100;
}
