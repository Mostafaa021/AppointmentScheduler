using AppointmentScheduler.Infrastructure.Auth;
using AppointmentScheduler.Infrastructure.Idempotency;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppointmentScheduler.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire runs inside the Api process (ADR-0014), storing jobs in the same PostgreSQL database (schema <c>hangfire</c>).
/// <c>BackgroundJobs:Enabled=false</c> skips the server and recurring registrations; jobs can still be
/// resolved and run directly.
/// </summary>
public static class BackgroundJobsSetup
{
    private const string EnabledKey = "BackgroundJobs:Enabled";

    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        services.AddHangfire(c => c
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString), new PostgreSqlStorageOptions { SchemaName = "hangfire" }));

        if (configuration.GetValue(EnabledKey, true)) services.AddHangfireServer();

        return services
            .AddScoped<PurgeExpiredRefreshTokensJob>()
            .AddScoped<PurgeExpiredIdempotencyRecordsJob>();
    }

    /// <summary>Registers recurring jobs and, in Development, the dashboard at <c>/hangfire</c> (local requests only).</summary>
    public static IApplicationBuilder UseBackgroundJobs(this IApplicationBuilder app, IConfiguration configuration, bool isDevelopment)
    {
        if (!configuration.GetValue(EnabledKey, true)) return app;

        var recurring = app.ApplicationServices.GetRequiredService<IRecurringJobManager>();
        recurring.AddOrUpdate<PurgeExpiredRefreshTokensJob>(
            PurgeExpiredRefreshTokensJob.RecurringJobId, job => job.RunAsync(CancellationToken.None), Cron.Hourly());
        recurring.AddOrUpdate<PurgeExpiredIdempotencyRecordsJob>(
            PurgeExpiredIdempotencyRecordsJob.RecurringJobId, job => job.RunAsync(CancellationToken.None), Cron.Hourly());

        // Browser navigation can't carry our in-memory JWT, so the dashboard is a local developer tool only.
        if (isDevelopment) app.UseHangfireDashboard("/hangfire");
        return app;
    }
}
