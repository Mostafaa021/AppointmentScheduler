using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// MVC's client-error results (415 from <c>[FromBody]</c> with a non-JSON body, and similar) get the <c>ApiResponse</c> envelope
/// instead of ProblemDetails. Without this they are bodiless with <c>Content-Length: 0</c>, which status-code pages skip.
/// </summary>
internal sealed class ApiResponseClientErrorFactory : IClientErrorFactory
{
    public IActionResult GetClientError(ActionContext actionContext, IClientErrorActionResult clientError)
    {
        var status = clientError.StatusCode ?? StatusCodes.Status400BadRequest;
        var (code, message) = ErrorStatusMapping.For(status);
        return new ObjectResult(ApiErrorWriter.Body(actionContext.HttpContext, status, code, message)) { StatusCode = status };
    }
}
