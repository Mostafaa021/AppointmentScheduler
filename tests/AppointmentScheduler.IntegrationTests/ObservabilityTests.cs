using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace AppointmentScheduler.IntegrationTests;

/// <summary>ADR-0010: tracing is wired (even with no exporter in tests) and request spans carry ids, never PII.</summary>
[Collection(nameof(ApiCollection))]
public sealed class ObservabilityTests(ApiFactory api) : IDisposable
{
    private readonly ConcurrentQueue<Activity> _stopped = new();
    private ActivityListener? _listener;

    [Fact]
    public void Tracer_provider_is_registered() =>
        Assert.NotNull(api.Services.GetService<TracerProvider>());

    [Fact]
    public async Task Request_span_is_tagged_with_the_user_id_only()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);

        var client = await api.ClientForAsync(TestUsers.Receptionist);
        var me = await (await client.GetAsync("/api/v1/me")).DataAsync();
        var userId = me.GetProperty("id").GetString();

        var span = Assert.Single(_stopped, a => a.GetTagItem("enduser.id") as string == userId && a.DisplayName.Contains("me", StringComparison.Ordinal));
        Assert.DoesNotContain(span.TagObjects, t => t.Value is string v && v.Contains(TestUsers.Receptionist, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose() => _listener?.Dispose();
}
