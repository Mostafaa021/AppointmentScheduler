using System.Diagnostics;
using AppointmentScheduler.Api.Contracts;
using Microsoft.Extensions.Options;

namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// Builds and writes the <see cref="ApiResponse{T}"/> error body for every path that does not go through an action result:
/// the global exception handler, JWT challenge/forbid and bodiless status codes. One place for the traceId and JSON options.
/// </summary>
internal static class ApiErrorWriter
{
    public static ApiResponse<object> Body(HttpContext http, int statusCode, string errorCode, string message,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        ApiResponse<object>.Fail(statusCode, errorCode, message, errors, TraceId(http));

    public static Task WriteAsync(HttpContext http, int statusCode, string errorCode, string message,
        IReadOnlyDictionary<string, string[]>? errors = null, CancellationToken ct = default)
    {
        http.Response.StatusCode = statusCode;
        // MVC's options, so these bodies serialise exactly like controller results (camelCase, enums as strings, no nulls).
        var json = http.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
        return http.Response.WriteAsJsonAsync(Body(http, statusCode, errorCode, message, errors), json, ct);
    }

    public static string TraceId(HttpContext http) => Activity.Current?.Id ?? http.TraceIdentifier;
}
