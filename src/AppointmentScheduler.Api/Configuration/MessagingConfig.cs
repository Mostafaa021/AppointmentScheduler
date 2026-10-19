using AppointmentScheduler.Infrastructure.Messaging;

namespace AppointmentScheduler.Api.Configuration;

public static class MessagingConfig
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Wolverine + RabbitMQ with the PostgreSQL message store (ADR-0007). The wiring lives in Infrastructure
        /// (<c>Messaging/MessagingSetup.cs</c>); this keeps <c>Program.cs</c> one chain of <c>Add…Configuration()</c> calls.
        /// </summary>
        public WebApplicationBuilder AddMessagingConfiguration()
        {
            builder.AddMessaging();
            return builder;
        }
    }
}
