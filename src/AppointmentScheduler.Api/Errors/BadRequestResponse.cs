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
            ApiErrorCodes.BadRequest, "The request could not be read.", errors));
    }
}
