using AppointmentScheduler.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// The single place where exceptions become HTTP responses (<see cref="Contracts.ApiResponse{T}"/>, ADR-0015).
/// Expected failures (<see cref="SchedulerException"/>, unreadable requests) carry their own status and code; anything else
/// is logged and returned as a generic 500 that never exposes the exception message or stack trace. Client aborts never
/// reach this handler: ExceptionHandlerMiddleware already answers them with 499 and a Debug log.
/// </summary>
internal sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        switch (exception)
        {
            case SchedulerException e:
                LogRejected(logger, http.Request.Method, http.Request.Path, e.StatusCode, e.ErrorCode, e.LogDetail);
                await ApiErrorWriter.WriteAsync(http, e.StatusCode, e.ErrorCode, e.Message, (e as ValidationException)?.Errors, ct);
                break;

            case BadHttpRequestException e:
                await ApiErrorWriter.WriteAsync(http, e.StatusCode, ErrorCodes.BadRequest, e.Message, ct: ct);
                break;

            default:
                // Unexpected: log everything for operators, return nothing internal to the client.
                LogUnhandled(logger, exception, http.Request.Method, http.Request.Path, http.TraceIdentifier);
                await ApiErrorWriter.WriteAsync(http, StatusCodes.Status500InternalServerError, ErrorCodes.InternalError,
                    "An unexpected error occurred. Quote the traceId when reporting it.", ct: ct);
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
