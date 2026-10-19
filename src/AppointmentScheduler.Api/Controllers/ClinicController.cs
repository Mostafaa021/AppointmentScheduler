using AppointmentScheduler.Api.Contracts;
using AppointmentScheduler.Application.Clinics;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/clinic")]
public sealed class ClinicController(ClinicQueries clinic) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("The clinic's time zone, opening hours and today's clinic-local date (the UI's starting day).")]
    [ProducesResponseType(typeof(ApiResponse<ClinicDto>), StatusCodes.Status200OK)]
    public IActionResult Get() => OkResponse(clinic.Get());
}
