namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// The <c>errorCode</c> and default message for errors that the framework, not our code, produces (unknown route, wrong
/// method, wrong media type, Asp.Versioning rejections). Shared by <see cref="ApiResponseProblemDetailsWriter"/> and
/// <see cref="ApiResponseClientErrorFactory"/>, so both paths answer identically.
/// </summary>
internal static class ErrorStatusMapping
{
    public const string ServerErrorMessage = "An unexpected error occurred. Quote the traceId when reporting it.";
    public const string ServiceUnavailableMessage = "The service is temporarily unavailable. Please retry shortly.";

    public static (string Code, string Message) For(int status, string? problemType = null) => status switch
    {
        // Asp.Versioning identifies its errors by problem type (…/unsupported-api-version, …/invalid-api-version, …).
        _ when problemType?.Contains("api-version", StringComparison.OrdinalIgnoreCase) == true =>
            (ApiErrorCodes.UnsupportedApiVersion, "The requested API version is not supported."),
        StatusCodes.Status401Unauthorized => (ApiErrorCodes.Unauthorized, "Authentication is required."),
        StatusCodes.Status403Forbidden => (ApiErrorCodes.Forbidden, "You are not allowed to access this resource."),
        StatusCodes.Status404NotFound => (ApiErrorCodes.RouteNotFound, "No endpoint matches this URL."),
        StatusCodes.Status405MethodNotAllowed => (ApiErrorCodes.MethodNotAllowed, "This endpoint does not support the HTTP method."),
        StatusCodes.Status415UnsupportedMediaType => (ApiErrorCodes.UnsupportedMediaType, "Send the request body as application/json."),
        StatusCodes.Status503ServiceUnavailable => (ApiErrorCodes.ServiceUnavailable, ServiceUnavailableMessage),
        >= 500 => (ApiErrorCodes.InternalError, ServerErrorMessage),
        _ => (ApiErrorCodes.BadRequest, "The request could not be processed."),
    };
}