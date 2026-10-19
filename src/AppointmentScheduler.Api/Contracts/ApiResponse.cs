using System.Text.Json.Serialization;

namespace AppointmentScheduler.Api.Contracts;

/// <summary>
/// The body of every API response (ADR-0015). Success: <c>success</c>, <c>statusCode</c>, optional <c>message</c> and
/// <c>data</c>. Failure: <c>errorCode</c> from <see cref="Domain.Common.ErrorCodes"/>, a safe <c>message</c>, field
/// <c>errors</c> for 400s and the <c>traceId</c> to quote. Unused envelope members are not serialised; nulls inside
/// <c>data</c> are (the DTO contract is unchanged).
/// </summary>
public sealed record ApiResponse<T>
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; init; }

#pragma warning disable CA1000 // ApiResponse<T>.Ok(...) at the call site is the point of the type
    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, StatusCode = StatusCodes.Status200OK, Message = message, Data = data };

    public static ApiResponse<T> Created(T data, string? message = null) =>
        new() { Success = true, StatusCode = StatusCodes.Status201Created, Message = message, Data = data };

    public static ApiResponse<T> Fail(int statusCode, string errorCode, string message,
        IReadOnlyDictionary<string, string[]>? errors = null, string? traceId = null) =>
        new() { Success = false, StatusCode = statusCode, ErrorCode = errorCode, Message = message, Errors = errors, TraceId = traceId };
#pragma warning restore CA1000
}
