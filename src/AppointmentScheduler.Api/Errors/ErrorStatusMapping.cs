using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// The <c>errorCode</c> and default message for errors that the framework, not our code, produces (unknown route, wrong
/// method, wrong media type, Asp.Versioning rejections). Shared by <see cref="ApiResponseProblemDetailsWriter"/> and
/// <see cref="ApiResponseClientErrorFactory"/>, so both paths answer identically.
/// </summary>
internal static class ErrorStatusMapping
{
    public const string ServerErrorMessage = "An unexpected error occurred. Quote the traceId when reporting it.";

    public static (string Code, string Message) For(int status, string? problemType = null) => status switch
    {
        // Asp.Versioning identifies its errors by problem type (…/unsupported-api-version, …/invalid-api-version, …).
        _ when problemType?.Contains("api-version", StringComparison.OrdinalIgnoreCase) == true =>
            (ErrorCodes.UnsupportedApiVersion, "The requested API version is not supported."),
        StatusCodes.Status401Unauthorized => (ErrorCodes.Unauthorized, "Authentication is required."),
        StatusCodes.Status403Forbidden => (ErrorCodes.Forbidden, "You are not allowed to access this resource."),
        StatusCodes.Status404NotFound => (ErrorCodes.RouteNotFound, "No endpoint matches this URL."),
        StatusCodes.Status405MethodNotAllowed => (ErrorCodes.MethodNotAllowed, "This endpoint does not support the HTTP method."),
        StatusCodes.Status415UnsupportedMediaType => (ErrorCodes.UnsupportedMediaType, "Send the request body as application/json."),
        >= 500 => (ErrorCodes.InternalError, ServerErrorMessage),
        _ => (ErrorCodes.BadRequest, "The request could not be processed."),
    };
}
