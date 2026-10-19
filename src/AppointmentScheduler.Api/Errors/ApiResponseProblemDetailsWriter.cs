namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// Registered ahead of the default writer, so every ProblemDetails the framework produces through
/// <c>IProblemDetailsService</c> is written as an <c>ApiResponse</c> error instead (ADR-0015). That covers bodiless statuses
/// from <c>UseStatusCodePages</c> and Asp.Versioning rejections. Exceptions don't come here: <see cref="GlobalExceptionHandler"/>
/// writes those.
/// </summary>
internal sealed class ApiResponseProblemDetailsWriter : IProblemDetailsWriter
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
        return new ValueTask(ApiErrorWriter.WriteAsync(http, status, code, message));
    }
}
