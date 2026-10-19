using AppointmentScheduler.Application;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Infrastructure;

namespace AppointmentScheduler.Api.Configuration;

internal static class CoreConfiguration
{
    /// <summary>
    /// The layers behind the API: clinic options (validated at startup), the clock, Application services and Infrastructure
    /// (persistence, repositories, Identity + JWT, Hangfire).
    /// </summary>
    public static WebApplicationBuilder AddCoreConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<ClinicOptions>()
            .Bind(builder.Configuration.GetSection(ClinicOptions.Section))
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Clinic:TimeZone must be a valid IANA zone.")
            .ValidateOnStart();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        return builder;
    }
}
