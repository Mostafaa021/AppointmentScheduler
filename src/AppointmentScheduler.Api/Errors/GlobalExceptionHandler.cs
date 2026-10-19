using AppointmentScheduler.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// The single place where exceptions become HTTP responses (RFC 7807 ProblemDetails with a stable <c>code</c>).
/// Expected failures (domain exceptions, unreadable requests) map to 4xx; anything else is logged and returned as a
/// generic 500 that never exposes the exception message or stack trace. Client aborts never reach this handler:
/// ExceptionHandlerMiddleware already answers them with 499 and a Debug log.
/// </summary>
internal sealed partial class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            ValidationException v => Problem(new HttpValidationProblemDetails(v.Errors.ToDictionary(e => e.Key, e => e.Value)),
                StatusCodes.Status400BadRequest, "Validation failed", v.Code, v.Message),
            BadHttpRequestException e => Problem(new ProblemDetails(), e.StatusCode, "Bad request", "bad_request", e.Message),
            UnauthorizedException e => Problem(new ProblemDetails(), StatusCodes.Status401Unauthorized, "Unauthorized", e.Code, e.Message),
            ForbiddenException e => Problem(new ProblemDetails(), StatusCodes.Status403Forbidden, "Forbidden", e.Code, e.Message),
            NotFoundException e => Problem(new ProblemDetails(), StatusCodes.Status404NotFound, "Not found", e.Code, e.Message),
            ConflictException e => Problem(new ProblemDetails(), StatusCodes.Status409Conflict, "Conflict", e.Code, e.Message),
            BusinessRuleException e => Problem(new ProblemDetails(), StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Code, e.Message),
            LockedException e => Problem(new ProblemDetails(), StatusCodes.Status423Locked, "Locked", e.Code, e.Message),
            _ => null,
        };

        if (problem is null)
        {
            // Unexpected: log everything for operators, return nothing internal to the client.
            LogUnhandled(logger, exception, http.Request.Method, http.Request.Path, http.TraceIdentifier);
            problem = Problem(new ProblemDetails(), StatusCodes.Status500InternalServerError, "Internal server error",
                "internal_error", "An unexpected error occurred. Quote the traceId when reporting it.");
        }

        http.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem, Exception = exception });
    }

    private static ProblemDetails Problem(ProblemDetails problem, int status, string title, string code, string detail)
    {
        problem.Status = status;
        problem.Title = title;
        problem.Detail = detail;
        problem.Extensions["code"] = code;
        return problem;
    }

    // Method + path only: query strings and bodies may carry patient data (NFR-9).
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path} (trace {TraceIdentifier})")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, PathString path, string traceIdentifier);
}
