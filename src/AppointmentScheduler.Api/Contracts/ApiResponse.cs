using System.Text.Json.Serialization;

namespace AppointmentScheduler.Api.Contracts;

/// <summary>
/// Generic API response wrapper for all endpoints (ADR-0015). Same shape as the house template (<c>isSuccess</c>,
/// <c>statusCode</c>, <c>message</c>, <c>data</c>, <c>errorDetails</c>, <c>timestamp</c>), plus three members clients and
/// operators rely on: <c>errorCode</c> (stable, from <see cref="Domain.Common.ErrorCodes"/>), per-field <c>errors</c> for 400s
/// and the <c>traceId</c> to quote. Members that don't apply to a response are not serialised; nulls inside <c>data</c> are.
/// </summary>
/// <typeparam name="T">Type of data being returned</typeparam>
public class ApiResponse<T>
{
    /// <summary>Indicates if the request was successful.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>HTTP status code (repeated from the response line).</summary>
    public int StatusCode { get; set; }

    /// <summary>Message describing the result (the user-safe error message if failed).</summary>
    public string? Message { get; set; }

    /// <summary>The response data (absent if an error occurred).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; set; }

    /// <summary>
    /// Additional error details for unexpected exceptions, written in <b>Development only</b> (exception type and message).
    /// Always absent in Staging/Production: exception text can carry connection strings, SQL or patient data.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorDetails { get; set; }

    /// <summary>Stable machine-readable error code (e.g. <c>DOCTOR_CONFLICT</c>); absent on success.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    /// <summary>Field name → messages, for 400 <c>VALIDATION_FAILED</c> / <c>BAD_REQUEST</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; set; }

    /// <summary>W3C trace id of the request, on errors. Quote it when reporting a problem.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; set; }

    /// <summary>
    /// Timestamp when the response was generated. A wall-clock stamp for clients and support, not business time, so it reads
    /// the system clock instead of the injected <c>TimeProvider</c> the domain uses.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = TimeProvider.System.GetUtcNow();

#pragma warning disable CA1000 // ApiResponse<T>.Success(...) at the call site is the point of the type

    /// <summary>Creates a successful response.</summary>
    public static ApiResponse<T> Success(T data, string? message = null, int statusCode = 200) => new()
    {
        IsSuccess = true,
        StatusCode = statusCode,
        Message = message ?? "Request completed successfully",
        Data = data,
    };

    /// <summary>Creates an error response.</summary>
    public static ApiResponse<T> Error(string message, int statusCode = 500, string? errorDetails = null) => new()
    {
        IsSuccess = false,
        StatusCode = statusCode,
        Message = message,
        ErrorDetails = errorDetails,
        Data = default,
    };

    /// <summary>Creates an error response with its stable code, field errors and trace id, as the error pipeline writes it.</summary>
    public static ApiResponse<T> Error(int statusCode, string errorCode, string message,
        IReadOnlyDictionary<string, string[]>? errors = null, string? traceId = null, string? errorDetails = null)
    {
        var response = Error(message, statusCode, errorDetails);
        response.ErrorCode = errorCode;
        response.Errors = errors;
        response.TraceId = traceId;
        return response;
    }

    /// <summary>Creates a not found response.</summary>
    public static ApiResponse<T> NotFound(string message = "Resource not found") => Error(message, 404);

    /// <summary>Creates a bad request response.</summary>
    public static ApiResponse<T> BadRequest(string message = "Invalid request") => Error(message, 400);

    /// <summary>Creates a created response (201).</summary>
    public static ApiResponse<T> Created(T data, string? message = null) =>
        Success(data, message ?? "Resource created successfully", 201);

    /// <summary>
    /// Creates a no content response (204). A real HTTP 204 carries no body, so endpoints that answer 204 (logout) return
    /// <c>NoContent()</c>; this factory is for callers that need the envelope itself with status 204.
    /// </summary>
    public static ApiResponse<T> NoContent(string? message = null) => new()
    {
        IsSuccess = true,
        StatusCode = 204,
        Message = message ?? "Operation completed successfully",
        Data = default,
    };

#pragma warning restore CA1000
}
