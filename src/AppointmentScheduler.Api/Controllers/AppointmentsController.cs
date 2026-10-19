using AppointmentScheduler.Api.Auth;
using AppointmentScheduler.Api.Contracts;
using AppointmentScheduler.Application.Appointments;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Controllers;

// Policies AND-combine: the POSTs need ScheduleReader and FrontDesk, so a Doctor gets 403 (AC-004-8).
// Reads are further limited to the doctor's own schedule inside AppointmentService (BR-7, ADR-0012).
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/appointments")]
[Authorize(Policy = Policies.ScheduleReader)]
public sealed class AppointmentsController(AppointmentService appointments) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("Day schedule of a doctor (clinic-local date), including cancelled appointments.")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppointmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DaySchedule(
        [FromQuery] int doctorId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var schedule = await appointments.GetDayScheduleAsync(doctorId, date, cancellationToken);
        return OkResponse(schedule);
    }

    [HttpGet("{id:guid}", Name = "GetAppointment")]
    [EndpointSummary("One appointment. 404 APPOINTMENT_NOT_FOUND, 403 FORBIDDEN for another doctor's appointment.")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var appointment = await appointments.GetAsync(id, cancellationToken);
        return OkResponse(appointment);
    }

    [HttpPost]
    [Authorize(Policy = Policies.FrontDesk)]
    [EndpointSummary("Book a free slot. 409 DOCTOR_CONFLICT / PATIENT_CONFLICT, 422 APPOINTMENT_IN_PAST / INVALID_SLOT.")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Book(
        [FromBody] BookAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await appointments.BookAsync(request, cancellationToken);
        return CreatedAtRoute("GetAppointment", new { id = created.Id },
            ApiResponse<AppointmentDto>.Created(created, "Appointment booked."));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.FrontDesk)]
    [EndpointSummary("Cancel a future appointment (soft, with optional reason). 422 APPOINTMENT_NOT_MODIFIABLE.")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid id,
        [FromBody] CancelAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var cancelled = await appointments.CancelAsync(id, request, cancellationToken);
        return OkResponse(cancelled, "Appointment cancelled.");
    }

    [HttpPost("{id:guid}/reschedule")]
    [Authorize(Policy = Policies.FrontDesk)]
    [EndpointSummary("Move a future appointment to another free slot of the same doctor. 409 / 422 as for booking.")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reschedule(
        [FromRoute] Guid id,
        [FromBody] RescheduleAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var rescheduled = await appointments.RescheduleAsync(id, request, cancellationToken);
        return OkResponse(rescheduled, "Appointment rescheduled.");
    }
}
