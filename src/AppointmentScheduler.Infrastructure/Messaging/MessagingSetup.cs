using AppointmentScheduler.Application.Appointments;
using JasperFx;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace AppointmentScheduler.Infrastructure.Messaging;

/// <summary>
/// Wolverine inside the Api process (ADR-0007, ADR-0014). Its inbox, outbox and scheduled/dead-letter tables live in the app's
/// PostgreSQL database (schema <c>wolverine</c>). RabbitMQ is attached only if it answers at startup; otherwise the host runs in
/// degraded mode (HTTP keeps working) and <see cref="RabbitMqRecoveryService"/> waits for the broker.
/// </summary>
public static class MessagingSetup
{
    public const string WolverineSchema = "wolverine";

    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var services = builder.Services;

        // Same database as the app, so a handler's EF changes and its outgoing messages commit in one transaction.
        var connectionString = configuration.GetConnectionString("Scheduler")
            ?? throw new InvalidOperationException("Connection string 'Scheduler' is missing (Wolverine message store).");

        // Bound once for startup wiring, and registered as IOptions for the recovery service.
        var section = configuration.GetSection(MessagingOptions.Section);
        var messaging = section.Get<MessagingOptions>() ?? new MessagingOptions();
        services.Configure<MessagingOptions>(section);

        var rabbit = RabbitMqAvailability.Probe(messaging.RabbitMq);
        services.AddSingleton(rabbit);

        builder.UseWolverine(opts =>
        {
            opts.ServiceName = "appointment-scheduler";
            opts.PersistMessagesWithPostgresql(connectionString, WolverineSchema);
            opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;
            opts.UseEntityFrameworkCoreTransactions();

            // Handlers are plain classes in Application (M5: AppointmentBooked → e-mail, reminders).
            opts.Discovery.IncludeAssembly(typeof(AppointmentService).Assembly);

            if (builder.Environment.IsDevelopment())
            {
                // One node: skip leader election so startup and recovery are immediate.
                opts.Durability.Mode = DurabilityMode.Solo;
                // The handler token is cancelled after DefaultExecutionTimeout (60 s); a breakpoint must not kill the message.
                opts.DefaultExecutionTimeout = TimeSpan.FromMinutes(30);
            }

            if (!rabbit.IsAvailable) return; // degraded mode: no RabbitMQ transport

            var rabbitMq = messaging.RabbitMq;
            opts.UseRabbitMq(factory =>
                {
                    factory.HostName = rabbitMq.Host;
                    factory.Port = rabbitMq.Port;
                    factory.UserName = rabbitMq.UserName;
                    factory.Password = rabbitMq.Password;
                })
                // We own every broker object we use, so Wolverine may declare them (exchange now, queues with M5 handlers).
                .AutoProvision()
                // Any queue Wolverine creates is a replicated, durable quorum queue.
                .UseQuorumQueues()
                // No request/reply over RabbitMQ (InvokeAsync<T> to remote nodes); skip the per-node response queue.
                .DisableSystemRequestReplyQueueDeclaration()
                .DeclareExchange(messaging.Publishing.Exchange, e => e.ExchangeType = ExchangeType.Topic);

            // ─── Publishing (M5) ────────────────────────────────────────────────────────────────────────
            // opts.PublishMessage<AppointmentBooked>().ToRabbitExchange(messaging.Publishing.Exchange).UseDurableOutbox();
        });

        if (!rabbit.IsAvailable && messaging.RabbitMq.Enabled)
            services.AddHostedService<RabbitMqRecoveryService>();

        return builder;
    }
}
