using System.Threading.RateLimiting;
using AppointmentScheduler.Api.RateLimiting;
using Polly;
using Polly.RateLimiting;

namespace AppointmentScheduler.Api.Configuration;

public static class RateLimitingConfig
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Rate limiting as Polly resilience pipelines (ADR-0009, NFR-6), one per policy, registered by name:
        /// <list type="bullet">
        /// <item><c>global</c>: concurrency limiter for the whole API;</item>
        /// <item><c>reads</c> / <c>writes</c>: sliding window / token bucket, partitioned by user or IP;</item>
        /// <item><c>auth</c>: fixed window per IP for login and refresh.</item>
        /// </list>
        /// The partitioned limiters read the request's partition from the Polly context (set by the middleware).
        /// </summary>
        public WebApplicationBuilder AddRateLimitingConfiguration()
        {
            var services = builder.Services;
            var options = builder.Configuration.GetSection(RateLimitingOptions.Section).Get<RateLimitingOptions>() ?? new();
            services.AddSingleton(options);
            services.AddSingleton<RateLimitMetrics>();

            services.AddResiliencePipeline(RateLimitPolicies.Global, b =>
                b.AddConcurrencyLimiter(options.ConcurrentRequests, queueLimit: 0));

            services.AddResiliencePipeline(RateLimitPolicies.Reads, (b, ctx) => Partitioned(b, ctx, key =>
                RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = options.Reads.PermitLimit,
                    Window = TimeSpan.FromSeconds(options.Reads.WindowSeconds),
                    SegmentsPerWindow = options.Reads.Segments,
                    QueueLimit = 0,
                })));

            services.AddResiliencePipeline(RateLimitPolicies.Writes, (b, ctx) => Partitioned(b, ctx, key =>
                RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = options.Writes.TokenLimit,
                    TokensPerPeriod = options.Writes.TokensPerPeriod,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(options.Writes.ReplenishmentSeconds),
                    QueueLimit = 0,
                })));

            services.AddResiliencePipeline(RateLimitPolicies.Auth, (b, ctx) => Partitioned(b, ctx, key =>
                RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.Auth.PermitLimit,
                    Window = TimeSpan.FromSeconds(options.Auth.WindowSeconds),
                    QueueLimit = 0,
                })));

            return builder;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>After authentication (partitions see the user), before authorization and the endpoints.</summary>
        public WebApplication UseRateLimitingConfiguration()
        {
            if (app.Services.GetRequiredService<RateLimitingOptions>().Enabled)
                app.UseMiddleware<PollyRateLimitingMiddleware>();
            return app;
        }
    }

    /// <summary>A Polly rate limiter whose limiter is partitioned by the key the middleware put in the context.</summary>
    private static void Partitioned<TContext>(
        ResiliencePipelineBuilder builder,
        Polly.DependencyInjection.AddResiliencePipelineContext<TContext> context,
        Func<string, RateLimitPartition<string>> partition)
        where TContext : notnull
    {
        var limiter = PartitionedRateLimiter.Create<string, string>(partition);
        context.OnPipelineDisposed(limiter.Dispose);

        builder.AddRateLimiter(new RateLimiterStrategyOptions
        {
            RateLimiter = args => limiter.AcquireAsync(
                args.Context.Properties.GetValue(RateLimitPolicies.PartitionKey, "unknown"), 1, args.Context.CancellationToken),
        });
    }
}
