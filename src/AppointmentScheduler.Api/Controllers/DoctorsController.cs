using AppointmentScheduler.Api.Auth;
using AppointmentScheduler.Api.Contracts;
using AppointmentScheduler.Application.Availability;
using AppointmentScheduler.Application.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Controllers;

[Route("api/v1/doctors")]
[Authorize(Policy = Policies.FrontDesk)]
public sealed class DoctorsController(LookupQueries queries, AvailabilityService availability) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("All doctors, ordered by name.")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DoctorDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default)
    {
        var doctors = await queries.ListDoctorsAsync(cancellationToken);
        return OkResponse(doctors);
    }

    [HttpGet("{id:int}/availability")]
    [EndpointSummary("Slots of a doctor's day: Free, Booked, Past or PatientBusy (when patientId is given). 404 DOCTOR_NOT_FOUND.")]
    [ProducesResponseType(typeof(ApiResponse<AvailabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Availability(
        [FromRoute] int id,
        [FromQuery] DateOnly date,
        [FromQuery] int? patientId,
        CancellationToken cancellationToken = default)
    {
        var slots = await availability.GetAsync(id, date, patientId, cancellationToken);
        return OkResponse(slots);
    }
}
