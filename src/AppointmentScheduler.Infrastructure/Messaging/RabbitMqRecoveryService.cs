using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppointmentScheduler.Infrastructure.Messaging;

/// <summary>
/// Runs only when the broker was down at startup (degraded mode). Re-probes every <c>RecoveryIntervalMinutes</c>; once the
/// broker answers it stops the host (if <c>RestartOnRecovery</c>) so the orchestrator restarts it with the transport.
/// </summary>
internal sealed partial class RabbitMqRecoveryService(
    IOptions<MessagingOptions> options,
    RabbitMqState startupState,
    IHostEnvironment environment,
    IHostApplicationLifetime lifetime,
    TimeProvider clock,
    ILogger<RabbitMqRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var messaging = options.Value;
        var interval = TimeSpan.FromMinutes(Math.Max(1, messaging.RecoveryIntervalMinutes));
        var restart = messaging.RestartOnRecovery ?? !environment.IsDevelopment();
        LogDegraded(logger, startupState.Reason, interval);

        using var timer = new PeriodicTimer(interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!RabbitMqAvailability.Probe(messaging.RabbitMq).IsAvailable) continue;

            if (restart)
            {
                LogRestarting(logger);
                lifetime.StopApplication();
            }
            else
            {
                LogRecoveredRestartManually(logger);
            }

            return;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMQ unavailable at startup ({Reason}); messaging runs without the broker, re-checking every {Interval}")]
    private static partial void LogDegraded(ILogger logger, string reason, TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMQ is reachable again; stopping so the host restarts with the transport")]
    private static partial void LogRestarting(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMQ is reachable again; restart the API to attach the transport")]
    private static partial void LogRecoveredRestartManually(ILogger logger);
}
