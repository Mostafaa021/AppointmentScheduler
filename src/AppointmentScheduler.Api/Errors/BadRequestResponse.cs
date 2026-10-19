using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// Model-binding failures (malformed JSON, wrong types, unparsable <c>?date=</c>) → 400 ProblemDetails <c>bad_request</c>,
/// the same contract as before the move to controllers (AC-001-7, AC-004-22). Business validation is not done here.
/// </summary>
internal static class BadRequestResponse
{
    public static IActionResult FromModelState(ActionContext context)
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad request",
            Detail = "The request could not be read.",
            Extensions =
            {
                ["code"] = "bad_request",
                ["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier,
            },
        };
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }
}
