using System.Text.Json.Serialization;
using AppointmentScheduler.Api.Errors;
using AppointmentScheduler.Application;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Infrastructure;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace AppointmentScheduler.Api.Configuration;

public static class ApiConfig
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// The layers behind the API plus the MVC surface: clinic options, clock, Application + Infrastructure, controllers,
        /// JSON, the model-state 400 and the error pipeline (ADR-0015).
        /// </summary>
        public WebApplicationBuilder AddApiConfiguration()
        {
            var services = builder.Services;
            var configuration = builder.Configuration;

            // Clinic hours and zone: validated at startup so a bad IANA id never reaches a request.
            services.AddOptions<ClinicOptions>()
                .Bind(configuration.GetSection(ClinicOptions.Section))
                .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Clinic:TimeZone must be a valid IANA zone.")
                .ValidateOnStart();
            services.AddSingleton(TimeProvider.System);

            // Application + Infrastructure layers (persistence, repositories, Identity + JWT, Hangfire).
            services.AddApplication();
            services.AddInfrastructure(configuration);

            // Controllers. Required-ness is checked by FluentValidation in Application (400 VALIDATION_FAILED, ADR-0013),
            // so MVC must not reject missing non-nullable members first.
            services.AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
                .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
                // Unreadable input → 400 BAD_REQUEST envelope (AC-001-7).
                .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = BadRequestResponse.FromModelState);
            // MVC client-error results (415, …) → the same envelope instead of ProblemDetails.
            services.AddSingleton<IClientErrorFactory, ApiResponseClientErrorFactory>();

            // AddProblemDetails supplies the plumbing UseExceptionHandler and UseStatusCodePages expect, but the body we
            // write is still ApiResponse<T>: our writer is registered first, so it wins over the default one.
            services.AddSingleton<IProblemDetailsWriter, ApiResponseProblemDetailsWriter>();
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails();

            services.AddHealthChecks();
            return builder;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>Every exception and every bodiless error status becomes an <c>ApiResponse</c> error.</summary>
        public WebApplication UseApiExceptionHandling()
        {
            app.UseExceptionHandler();
            app.UseStatusCodePages();
            return app;
        }
    }
}
