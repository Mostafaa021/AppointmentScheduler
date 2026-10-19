using System.Security.Claims;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AppointmentScheduler.Api.Configuration;

public static class ObservabilityConfig
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// ADR-0010: OpenTelemetry traces (ASP.NET Core, HttpClient, EF Core, Wolverine) exported over OTLP to Jaeger, and
        /// ASP.NET Core / HttpClient metrics. Each exporter is on only when its endpoint is configured, so tests and
        /// machines without a collector export nothing.
        /// </summary>
        public WebApplicationBuilder AddObservabilityConfiguration()
        {
            var configuration = builder.Configuration;
            var serviceName = configuration["Observability:ServiceName"] ?? "appointment-scheduler-api";
            var otlpEndpoint = configuration["Observability:OtlpEndpoint"];
            var metricsEndpoint = configuration["Observability:MetricsEndpoint"];

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(serviceName: serviceName, serviceVersion: "1.0.0")
                    .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
                .WithTracing(tracing =>
                {
                    tracing
                        .AddAspNetCoreInstrumentation(o =>
                        {
                            // Probes and the dashboard's polling would bury the real requests.
                            o.Filter = http => !http.Request.Path.StartsWithSegments("/health")
                                               && !http.Request.Path.StartsWithSegments("/hangfire");
                            // The caller's id only (NFR-9: ids, never names or e-mails); set after authentication ran.
                            o.EnrichWithHttpResponse = (activity, response) =>
                            {
                                if (response.HttpContext.User.FindFirstValue("sub") is { } userId)
                                    activity.SetTag("enduser.id", userId);
                            };
                        })
                        .AddHttpClientInstrumentation()
                        .AddEntityFrameworkCoreInstrumentation()
                        .AddSource("Wolverine"); // send/receive/handler spans once messaging carries traffic (M5)

                    if (ToUri(otlpEndpoint) is { } endpoint)
                        tracing.AddOtlpExporter(o => o.Endpoint = endpoint);
                })
                .WithMetrics(metrics =>
                {
                    // Request rate / error rate / latency. Jaeger does not accept metrics, so they are exported only once
                    // an OTLP metrics backend (Grafana LGTM, SigNoz, …) is set in Observability:MetricsEndpoint.
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation();

                    if (ToUri(metricsEndpoint) is { } endpoint)
                        metrics.AddOtlpExporter(o => o.Endpoint = endpoint);
                });

            return builder;
        }
    }

    // The OTLP exporter needs an absolute URI; accept "host:4317" from config as http.
    private static Uri? ToUri(string? endpoint) =>
        string.IsNullOrWhiteSpace(endpoint) ? null
        : endpoint.Contains("://", StringComparison.Ordinal) ? new Uri(endpoint)
        : new Uri($"http://{endpoint}");
}
