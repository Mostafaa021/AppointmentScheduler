using AppointmentScheduler.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// Model-binding failures (malformed JSON, wrong types, unparsable <c>?date=</c>) → 400 <c>BAD_REQUEST</c> (AC-001-7,
/// AC-004-22). <c>[ApiController]</c> calls this before the action runs, so actions never check ModelState themselves.
/// Business validation is not done here (FluentValidation in Application, ADR-0013).
/// </summary>
internal static class BadRequestResponse
{
    public static IActionResult FromModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(e => e.Value is { Errors.Count: > 0 })
            .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());
        return new BadRequestObjectResult(ApiErrorWriter.Body(context.HttpContext, StatusCodes.Status400BadRequest,
            ErrorCodes.BadRequest, "The request could not be read.", errors));
    }

    /// <summary>Bodiless error statuses (unknown route, wrong method, …) get the same envelope as every other error.</summary>
    public static Task FromStatusCode(StatusCodeContext context)
    {
        var status = context.HttpContext.Response.StatusCode;
        var (code, message) = status switch
        {
            StatusCodes.Status401Unauthorized => (ErrorCodes.Unauthorized, "Authentication is required."),
            StatusCodes.Status403Forbidden => (ErrorCodes.Forbidden, "You are not allowed to access this resource."),
            StatusCodes.Status404NotFound => (ErrorCodes.RouteNotFound, "No endpoint matches this URL."),
            StatusCodes.Status405MethodNotAllowed => (ErrorCodes.MethodNotAllowed, "This endpoint does not support the HTTP method."),
            >= 500 => (ErrorCodes.InternalError, "An unexpected error occurred. Quote the traceId when reporting it."),
            _ => (ErrorCodes.BadRequest, "The request could not be processed."),
        };
        return ApiErrorWriter.WriteAsync(context.HttpContext, status, code, message);
    }
}
