using System.Text.Json.Serialization;
using AppointmentScheduler.Api.Errors;

namespace AppointmentScheduler.Api.Configuration;

internal static class ApiConfiguration
{
    /// <summary>Controllers, JSON, the model-state 400 and the global exception handler (ADR-0015).</summary>
    public static WebApplicationBuilder AddApiConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers(o =>
                // Required-ness is checked by FluentValidation in the Application layer (400 VALIDATION_FAILED, ADR-0013),
                // so MVC must not reject missing non-nullable members first.
                o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = BadRequestResponse.FromModelState);

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        return builder;
    }

    /// <summary>Every exception and every bodiless error status becomes an <c>ApiResponse</c> error.</summary>
    public static WebApplication UseApiExceptionHandling(this WebApplication app)
    {
        // GlobalExceptionHandler handles every exception; the middleware still demands a fallback without AddProblemDetails().
        app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = _ => Task.CompletedTask });
        app.UseStatusCodePages(BadRequestResponse.FromStatusCode); // bodiless 404/405/… → ApiResponse
        return app;
    }
}
