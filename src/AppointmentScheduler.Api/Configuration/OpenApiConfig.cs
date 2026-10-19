using AppointmentScheduler.Api.Idempotency;
using Scalar.AspNetCore;

namespace AppointmentScheduler.Api.Configuration;

public static class OpenApiConfig
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>Built-in .NET OpenAPI document (<c>v1</c>); every action declares its statuses (ADR-0015).</summary>
        public WebApplicationBuilder AddOpenApiConfiguration()
        {
            // Writes document their required Idempotency-Key header (spec 009).
            builder.Services.AddOpenApi(o => o.AddOperationTransformer<IdempotencyKeyOperationTransformer>());
            return builder;
        }
    }

    extension(WebApplication app)
    {
        /// <summary><c>/openapi/v1.json</c> and the Scalar UI at <c>/scalar</c>.</summary>
        public WebApplication MapOpenApiDocs()
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
            return app;
        }
    }
}
