using Scalar.AspNetCore;

namespace AppointmentScheduler.Api.Configuration;

internal static class OpenApiConfiguration
{
    public static WebApplicationBuilder AddOpenApiConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        return builder;
    }

    /// <summary><c>/openapi/v1.json</c> and the Scalar UI at <c>/scalar</c>.</summary>
    public static WebApplication MapOpenApiDocs(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
        return app;
    }
}
