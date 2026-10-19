using AppointmentScheduler.Api.Errors;
using Polly;
using Polly.Registry;
using Polly.RateLimiting;

namespace AppointmentScheduler.Api.RateLimiting;

/// <summary>
/// Runs every request inside Polly rate-limit pipelines (ADR-0009): the global concurrency cap, then the endpoint's policy
/// (<see cref="RateLimitPolicyAttribute"/>, or reads/writes by HTTP method), partitioned by user or IP. A rejection answers
/// 429 <c>RATE_LIMITED</c> with <c>Retry-After</c>; health probes are never limited.
/// </summary>
internal sealed partial class PollyRateLimitingMiddleware(
    RequestDelegate next,
    ResiliencePipelineProvider<string> pipelines,
    RateLimitMetrics metrics,
    ILogger<PollyRateLimitingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext http)
    {
        if (http.Request.Path.StartsWithSegments("/health"))
        {
            await next(http);
            return;
        }

        var policy = http.GetEndpoint()?.Metadata.GetMetadata<RateLimitPolicyAttribute>()?.Policy
                     ?? (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)
                         ? RateLimitPolicies.Reads
                         : RateLimitPolicies.Writes);
        var global = pipelines.GetPipeline(RateLimitPolicies.Global);
        var limited = pipelines.GetPipeline(policy);

        var context = ResilienceContextPool.Shared.Get(http.RequestAborted);
        context.Properties.Set(RateLimitPolicies.PartitionKey, RateLimitPartitionKey.For(http, policy));
        try
        {
            await global.ExecuteAsync(
                static async (ctx, state) => await state.limited.ExecuteAsync(
                    static async (_, s) => await s.next(s.http), ctx, state),
                context,
                (limited, next, http));
        }
        catch (RateLimiterRejectedException rejected)
        {
            await RejectAsync(http, policy, rejected.RetryAfter);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private async Task RejectAsync(HttpContext http, string policy, TimeSpan? retryAfter)
    {
        metrics.Rejected(policy);
        LogRejected(logger, policy, http.Request.Method, http.Request.Path);

        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter?.TotalSeconds ?? 1));
        http.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await ApiErrorWriter.WriteAsync(http, StatusCodes.Status429TooManyRequests, ApiErrorCodes.RateLimited,
            $"Too many requests. Retry after {seconds} second(s).", ct: http.RequestAborted);
    }

    // Method + path only (NFR-9); the partition (user id / IP) is deliberately not logged on every rejection.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit {Policy} rejected {Method} {Path}")]
    private static partial void LogRejected(ILogger logger, string policy, string method, PathString path);
}
