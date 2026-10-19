namespace AppointmentScheduler.Api.Security;

internal static class SecurityHeaders
{
    private const string ApiCsp = "default-src 'none'; frame-ancestors 'none'";

    /// <summary>AC-004-23. Registered first and applied in <c>OnStarting</c>, so error, 401 and 403 responses get them too.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use((http, next) =>
    {
        http.Response.OnStarting(() =>
        {
            var headers = http.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            if (http.Request.Path.StartsWithSegments("/api/v1")) headers.ContentSecurityPolicy = ApiCsp;
            return Task.CompletedTask;
        });
        return next(http);
    });
}
