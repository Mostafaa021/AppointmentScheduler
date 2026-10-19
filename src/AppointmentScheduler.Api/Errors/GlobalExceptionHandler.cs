using System.Data.Common;
using AppointmentScheduler.Application.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// The single place where exceptions become HTTP responses (<see cref="Contracts.ApiResponse{T}"/>, ADR-0015).
/// Expected failures (described by <see cref="ApplicationError"/>, plus unreadable requests) carry their own status and code.
/// A transient database failure is a 503 <c>SERVICE_UNAVAILABLE</c>: there is no retry (ADR-0008), the unit of work rolled
/// back, so the client retries. Anything else is logged and returned as a generic 500 that never exposes the exception
/// message or stack trace. Client aborts never reach this handler: ExceptionHandlerMiddleware already answers them with 499
/// and a Debug log.
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

            case var e when IsTransientDatabaseFailure(e):
                // The idempotency filter releases the key on 5xx, so the client's retry with the same key is safe.
                LogDatabaseUnavailable(logger, http.Request.Method, http.Request.Path, http.TraceIdentifier, e.GetType().Name);
                http.Response.Headers.RetryAfter = "5";
                await ApiErrorWriter.WriteAsync(http, StatusCodes.Status503ServiceUnavailable, ApiErrorCodes.ServiceUnavailable,
                    ErrorStatusMapping.ServiceUnavailableMessage, ct: ct);
                break;

            default:
                // Unexpected: log everything for operators. Clients get nothing internal, except errorDetails on a developer's
                // own machine (Development only; exception text can carry connection strings, SQL or patient data).
                LogUnhandled(logger, exception, http.Request.Method, http.Request.Path, http.TraceIdentifier);
                var details = environment.IsDevelopment() ? $"{exception.GetType().Name}: {exception.Message}" : null;
                await ApiErrorWriter.WriteAsync(http, StatusCodes.Status500InternalServerError, ApiErrorCodes.InternalError,
                    ErrorStatusMapping.ServerErrorMessage, errorDetails: details, ct: ct);
                break;
        }

        return true;
    }

    // Queries throw the provider's DbException directly; SaveChanges wraps it in DbUpdateException, so walk the chain.
    // DbException.IsTransient is BCL and Npgsql overrides it (broken connection, timeout): no provider reference needed.
    private static bool IsTransientDatabaseFailure(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is DbException { IsTransient: true })
                return true;
        }

        return false;
    }

    // Method + path only: query strings and bodies may carry patient data (NFR-9). LogDetail holds ids only.
    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected {Method} {Path} with {StatusCode} {ErrorCode} ({LogDetail})")]
    private static partial void LogRejected(ILogger logger, string method, PathString path, int statusCode, string errorCode, string? logDetail);

    // The exception type only: provider messages can carry SQL or connection details.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Database unavailable for {Method} {Path} (trace {TraceIdentifier}, {ExceptionType})")]
    private static partial void LogDatabaseUnavailable(ILogger logger, string method, PathString path, string traceIdentifier, string exceptionType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path} (trace {TraceIdentifier})")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, PathString path, string traceIdentifier);
}
