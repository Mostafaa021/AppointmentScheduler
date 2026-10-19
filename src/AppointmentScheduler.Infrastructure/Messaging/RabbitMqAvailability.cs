using RabbitMQ.Client;

namespace AppointmentScheduler.Infrastructure.Messaging;

/// <summary>Whether the RabbitMQ transport was attached at startup, and why not.</summary>
public sealed record RabbitMqState(bool IsAvailable, string Reason);

/// <summary>
/// One short AMQP connect (auth included) before Wolverine is configured. Wolverine cannot add the RabbitMQ transport to an
/// already running host, so the mode has to be decided up front.
/// </summary>
public static class RabbitMqAvailability
{
    public static RabbitMqState Probe(RabbitMqOptions options)
    {
        if (!options.Enabled) return new RabbitMqState(false, "disabled by Messaging:RabbitMq:Enabled");

        var timeout = TimeSpan.FromSeconds(options.ProbeTimeoutSeconds);
        var factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            RequestedConnectionTimeout = timeout,
            SocketReadTimeout = timeout,
            SocketWriteTimeout = timeout,
            AutomaticRecoveryEnabled = false,
        };

        try
        {
            // Startup-only, before the host exists: blocking here is fine and keeps the builder API synchronous.
            using var cts = new CancellationTokenSource(timeout);
            using var connection = factory.CreateConnectionAsync(cts.Token).GetAwaiter().GetResult();
            return new RabbitMqState(true, $"connected to {options.Host}:{options.Port}");
        }
#pragma warning disable CA1031 // any failure means "no broker"; the reason is logged and the host keeps serving HTTP
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return new RabbitMqState(false, $"{options.Host}:{options.Port} unreachable ({ex.GetType().Name})");
        }
    }
}
