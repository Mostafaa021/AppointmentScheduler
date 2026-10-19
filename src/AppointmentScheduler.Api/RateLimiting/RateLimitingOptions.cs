namespace AppointmentScheduler.Api.RateLimiting;

/// <summary>
/// Configuration section <c>RateLimiting</c> (NFR-6, ADR-0009). Defaults are the production numbers; tests and k6 runs override
/// them through configuration.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    /// <summary>False skips the middleware entirely (e.g. a gateway already limits).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Requests processed at once across the whole API; more are rejected immediately (no queue).</summary>
    public int ConcurrentRequests { get; set; } = 200;

    /// <summary>GET/HEAD per user (or IP): sliding window.</summary>
    public SlidingWindowSettings Reads { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60, Segments = 6 };

    /// <summary>Other methods per user (or IP): token bucket — a burst of <c>TokenLimit</c>, refilled to a steady 30/min.</summary>
    public TokenBucketSettings Writes { get; set; } = new() { TokenLimit = 10, TokensPerPeriod = 1, ReplenishmentSeconds = 2 };

    /// <summary>Login and refresh per client IP: fixed window, against password guessing (complements the account lockout).</summary>
    public FixedWindowSettings Auth { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };
}

public sealed class SlidingWindowSettings
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
    public int Segments { get; set; }
}

public sealed class TokenBucketSettings
{
    public int TokenLimit { get; set; }
    public int TokensPerPeriod { get; set; }
    public int ReplenishmentSeconds { get; set; }
}

public sealed class FixedWindowSettings
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}
