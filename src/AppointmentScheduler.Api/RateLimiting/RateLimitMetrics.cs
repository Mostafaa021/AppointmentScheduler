using System.Diagnostics.Metrics;

namespace AppointmentScheduler.Api.RateLimiting;

/// <summary>Counter <c>scheduler.rate_limit.rejected</c> (tag <c>policy</c>), on the <c>AppointmentScheduler</c> meter (ADR-0009/0010).</summary>
public sealed class RateLimitMetrics
{
    public const string MeterName = "AppointmentScheduler";

    private readonly Counter<long> _rejected;

    public RateLimitMetrics(IMeterFactory meters)
    {
        var meter = meters.Create(MeterName);
        _rejected = meter.CreateCounter<long>("scheduler.rate_limit.rejected", unit: "{request}",
            description: "Requests rejected by a rate-limit pipeline");
    }

    public void Rejected(string policy) => _rejected.Add(1, new KeyValuePair<string, object?>("policy", policy));
}
