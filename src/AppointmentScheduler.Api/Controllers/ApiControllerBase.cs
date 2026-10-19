using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Controllers;

/// <summary>
/// Base for every API controller: authenticated by default (AC-004-4); actions opt out with <c>[AllowAnonymous]</c>
/// and narrow access with role policies (AC-004-8). Errors are thrown as domain exceptions and shaped by GlobalExceptionHandler.
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase;
