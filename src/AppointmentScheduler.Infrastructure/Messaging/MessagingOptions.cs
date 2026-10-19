namespace AppointmentScheduler.Infrastructure.Messaging;

/// <summary>Configuration section <c>Messaging</c> (ADR-0007). Secrets come from env (<c>Messaging__RabbitMq__Password</c>).</summary>
public sealed class MessagingOptions
{
    public const string Section = "Messaging";

    public RabbitMqOptions RabbitMq { get; set; } = new();

    public PublishingOptions Publishing { get; set; } = new();

    /// <summary>How often a degraded host (broker down at startup) re-checks the broker.</summary>
    public int RecoveryIntervalMinutes { get; set; } = 1;

    /// <summary>
    /// Stop the host when the broker comes back, so the orchestrator restarts it with the transport (Wolverine cannot attach
    /// RabbitMQ to a running host). Null = true outside Development; in Development a restart would kill <c>dotnet run</c>.
    /// </summary>
    public bool? RestartOnRecovery { get; set; }
}

public sealed class RabbitMqOptions
{
    /// <summary>False runs Wolverine with its PostgreSQL store only (machines without a broker).</summary>
    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "";

    public int ProbeTimeoutSeconds { get; set; } = 3;
}

public sealed class PublishingOptions
{
    /// <summary>Topic exchange this service publishes its integration events to (M5: appointment.booked, …).</summary>
    public string Exchange { get; set; } = "appointment-scheduler.events";
}
