using AppointmentScheduler.Application.Appointments;
using AppointmentScheduler.Application.Availability;
using AppointmentScheduler.Application.Lookups;
using Microsoft.Extensions.DependencyInjection;

namespace AppointmentScheduler.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddScoped<AppointmentService>()
        .AddScoped<AvailabilityService>()
        .AddScoped<LookupQueries>();
}
