using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// Registered ahead of the default writer, so every ProblemDetails the framework produces through
/// <c>IProblemDetailsService</c> is written as an <c>ApiResponse</c> error instead (ADR-0015). That covers bodiless statuses
/// from <c>UseStatusCodePages</c> and Asp.Versioning rejections. Exceptions don't come here: <see cref="GlobalExceptionHandler"/>
/// writes those.
/// </summary>
internal sealed class ApiResponseProblemDetailsWriter(IApiVersionDescriptionProvider versions) : IProblemDetailsWriter
{
    public bool CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var http = context.HttpContext;
        var problem = context.ProblemDetails;
        var status = problem.Status ?? http.Response.StatusCode;
        var (code, fallback) = ErrorStatusMapping.For(status, problem.Type);

        // 5xx details may describe internals; 4xx details from the framework are written for clients.
        var message = status >= 500 || string.IsNullOrWhiteSpace(problem.Detail) ? fallback : problem.Detail;

        // Asp.Versioning answers an unknown URL-segment version with a plain 404; say why (ADR-0017).
        if (status == StatusCodes.Status404NotFound && UnsupportedUrlVersion(http.Request.Path) is { } requested)
        {
            code = ErrorCodes.UnsupportedApiVersion;
            message = $"API version '{requested}' is not supported. Supported versions: {Supported()}.";
        }

        return new ValueTask(ApiErrorWriter.WriteAsync(http, status, code, message));
    }

    /// <summary>The version in <c>/api/v{version}/…</c> when it parses but no controller offers it; otherwise null.</summary>
    private ApiVersion? UnsupportedUrlVersion(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (segments is not ["api", var segment, ..] || segment.Length < 2 || segment[0] != 'v') return null;
        if (!ApiVersionParser.Default.TryParse(segment.AsSpan(1), out var requested) || requested is null) return null;
        return versions.ApiVersionDescriptions.Any(d => d.ApiVersion == requested) ? null : requested;
    }

    private string Supported() => string.Join(", ", versions.ApiVersionDescriptions.Select(d => d.ApiVersion.ToString()));
}
