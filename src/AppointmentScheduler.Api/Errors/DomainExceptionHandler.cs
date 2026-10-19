using AppointmentScheduler.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// Maps expected failures to RFC 7807 ProblemDetails with a stable <c>code</c> extension:
/// domain exceptions and request-binding errors (malformed JSON, bad query values). Anything else falls through to a generic 500.
/// </summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            ValidationException v => Problem(new HttpValidationProblemDetails(v.Errors.ToDictionary(e => e.Key, e => e.Value)), 400, "Validation failed", v),
            NotFoundException e => Problem(new ProblemDetails(), 404, "Not found", e),
            ConflictException e => Problem(new ProblemDetails(), 409, "Conflict", e),
            BusinessRuleException e => Problem(new ProblemDetails(), 422, "Business rule violated", e),
            UnauthorizedException e => Problem(new ProblemDetails(), 401, "Unauthorized", e),
            ForbiddenException e => Problem(new ProblemDetails(), 403, "Forbidden", e),
            LockedException e => Problem(new ProblemDetails(), 423, "Locked", e),
            BadHttpRequestException e => new ProblemDetails { Status = e.StatusCode, Title = "Bad request", Detail = e.Message, Extensions = { ["code"] = "bad_request" } },
            _ => null,
        };
        if (problem is null) return false;

        http.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem, Exception = exception });
    }

    private static ProblemDetails Problem(ProblemDetails problem, int status, string title, DomainException e)
    {
        problem.Status = status;
        problem.Title = title;
        problem.Detail = e.Message;
        problem.Extensions["code"] = e.Code;
        return problem;
    }
}
