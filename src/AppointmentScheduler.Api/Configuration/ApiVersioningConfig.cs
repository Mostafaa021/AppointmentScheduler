using Asp.Versioning;

namespace AppointmentScheduler.Api.Configuration;

public static class ApiVersioningConfig
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// ADR-0017: every controller declares its version (<c>[ApiVersion(1.0)]</c>) and routes carry it in the URL
        /// (<c>api/v{version:apiVersion}/…</c>). The header and query readers accept the same version for clients that send
        /// it there; an unknown version becomes the <c>UNSUPPORTED_API_VERSION</c> envelope (ApiResponseProblemDetailsWriter).
        /// </summary>
        public WebApplicationBuilder AddApiVersioningConfiguration()
        {
            builder.Services
                .AddApiVersioning(options =>
                {
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.ReportApiVersions = true; // api-supported-versions / api-deprecated-versions headers
                    options.ApiVersionReader = ApiVersionReader.Combine(
                        new UrlSegmentApiVersionReader(),
                        new HeaderApiVersionReader("X-Api-Version"),
                        new QueryStringApiVersionReader("api-version"));
                })
                .AddMvc()
                .AddApiExplorer(options =>
                {
                    // Group "v1" matches the OpenAPI document name; /api/v{version} is shown as /api/v1 in the docs.
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });

            return builder;
        }
    }
}
