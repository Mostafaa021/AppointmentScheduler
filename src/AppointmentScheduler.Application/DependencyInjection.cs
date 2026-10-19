using AppointmentScheduler.Application.Appointments;
using AppointmentScheduler.Application.Auth;
using AppointmentScheduler.Application.Availability;
using AppointmentScheduler.Application.Clinics;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Application.Lookups;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AppointmentScheduler.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddScoped<AppointmentService>()
        .AddScoped<AvailabilityService>()
        .AddScoped<LookupQueries>()
        .AddScoped<ClinicQueries>()
        .AddScoped<AuditLog>()
        // Registered by hand, no DI-extensions package (ADR-0013). Validators are stateless.
        .AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>()
        .AddSingleton<IValidator<CreateUserRequest>, CreateUserRequestValidator>()
        .AddSingleton<IValidator<SetUserRolesRequest>, SetUserRolesRequestValidator>()
        .AddSingleton<IValidator<BookAppointmentRequest>, BookAppointmentRequestValidator>()
        .AddSingleton<IValidator<CancelAppointmentRequest>, CancelAppointmentRequestValidator>()
        .AddSingleton<IValidator<RescheduleAppointmentRequest>, RescheduleAppointmentRequestValidator>();
}
