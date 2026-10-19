using Polly;

namespace AppointmentScheduler.Api.RateLimiting;

/// <summary>Names of the Polly rate-limit pipelines (registered by <c>RateLimitingConfig</c>, run by the middleware).</summary>
public static class RateLimitPolicies
{
    /// <summary>Concurrency cap for every request, outermost.</summary>
    public const string Global = "rate-limit:global";

    /// <summary>GET/HEAD, per user or IP.</summary>
    public const string Reads = "rate-limit:reads";

    /// <summary>Every other method, per user or IP.</summary>
    public const string Writes = "rate-limit:writes";

    /// <summary>Login and refresh, per IP; chosen with <see cref="RateLimitPolicyAttribute"/>.</summary>
    public const string Auth = "rate-limit:auth";

    /// <summary>The partition (user or IP) of the current request, passed to the partitioned limiters through Polly's context.</summary>
    internal static readonly ResiliencePropertyKey<string> PartitionKey = new("rate-limit.partition");
}

/// <summary>Runs an action under a specific rate-limit pipeline instead of the method-based default (reads/writes).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RateLimitPolicyAttribute(string policy) : Attribute
{
    public string Policy { get; } = policy;
}
