using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Application.Common;

/// <summary>
/// An expected failure, described for the host without exposing Domain types. The Api never references Domain: it asks
/// <see cref="From"/> whether an exception is one of ours and writes the answer as an <c>ApiResponse</c> error.
/// </summary>
/// <param name="StatusCode">HTTP status the failure maps to (400, 401, 403, 404, 409, 422, 423).</param>
/// <param name="ErrorCode">Stable machine-readable code (UPPER_SNAKE, Domain <c>ErrorCodes</c>).</param>
/// <param name="Message">User-safe message.</param>
/// <param name="FieldErrors">Field name → messages, for validation failures only.</param>
/// <param name="LogDetail">Log-only diagnostics (ids, constraint names); never sent to clients.</param>
public sealed record ApplicationError(
    int StatusCode,
    string ErrorCode,
    string Message,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    string? LogDetail)
{
    /// <summary>The described failure, or null when <paramref name="exception"/> is unexpected (→ generic 500).</summary>
    public static ApplicationError? From(Exception exception) => exception is SchedulerException e
        ? new ApplicationError(e.StatusCode, e.ErrorCode, e.Message, (e as ValidationException)?.Errors, e.LogDetail)
        : null;
}
