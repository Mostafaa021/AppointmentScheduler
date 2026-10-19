using AppointmentScheduler.Application.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// The single place where exceptions become HTTP responses (<see cref="Contracts.ApiResponse{T}"/>, ADR-0015).
/// Expected failures (described by <see cref="ApplicationError"/>, plus unreadable requests) carry their own status and code; anything else
/// is logged and returned as a generic 500 that never exposes the exception message or stack trace. Client aborts never
/// reach this handler: ExceptionHandlerMiddleware already answers them with 499 and a Debug log.
/// </summary>
internal sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        // Application describes its own failures, so the Api never needs the Domain exception types.
        if (ApplicationError.From(exception) is { } error)
        {
            LogRejected(logger, http.Request.Method, http.Request.Path, error.StatusCode, error.ErrorCode, error.LogDetail);
            await ApiErrorWriter.WriteAsync(http, error.StatusCode, error.ErrorCode, error.Message, error.FieldErrors, ct: ct);
            return true;
        }

        switch (exception)
        {
            case BadHttpRequestException e:
                await ApiErrorWriter.WriteAsync(http, e.StatusCode, ApiErrorCodes.BadRequest, e.Message, ct: ct);
                break;

            default:
                // Unexpected: log everything for operators. Clients get nothing internal, except errorDetails on a developer's
                // own machine (Development only; exception text can carry connection strings, SQL or patient data).
                LogUnhandled(logger, exception, http.Request.Method, http.Request.Path, http.TraceIdentifier);
                var details = environment.IsDevelopment() ? $"{exception.GetType().Name}: {exception.Message}" : null;
                await ApiErrorWriter.WriteAsync(http, StatusCodes.Status500InternalServerError, ApiErrorCodes.InternalError,
                    "An unexpected error occurred. Quote the traceId when reporting it.", errorDetails: details, ct: ct);
                break;
        }

        return true;
    }

    // Method + path only: query strings and bodies may carry patient data (NFR-9). LogDetail holds ids only.
    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected {Method} {Path} with {StatusCode} {ErrorCode} ({LogDetail})")]
    private static partial void LogRejected(ILogger logger, string method, PathString path, int statusCode, string errorCode, string? logDetail);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path} (trace {TraceIdentifier})")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, PathString path, string traceIdentifier);
}
