using AppointmentScheduler.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Controllers;

/// <summary>
/// Base for every API controller: authenticated by default (AC-004-4); actions opt out with <c>[AllowAnonymous]</c>
/// and narrow access with role policies (AC-004-8). Every body is an <see cref="ApiResponse{T}"/> (ADR-0015).
/// Errors are thrown by the Application layer and shaped by GlobalExceptionHandler, so actions never build error
/// results. 401 and 500 can happen on any action and are declared here; each action declares the rest.
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>200 with <paramref name="data"/> wrapped in <see cref="ApiResponse{T}"/>.</summary>
    protected OkObjectResult OkResponse<T>(T data, string? message = null) => Ok(ApiResponse<T>.Success(data, message));
}
