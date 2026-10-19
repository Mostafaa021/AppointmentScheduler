using AppointmentScheduler.Api.Auth;
using AppointmentScheduler.Application.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Controllers;

// Policies AND-combine: the POSTs need ScheduleReader and FrontDesk, so a Doctor gets 403 (AC-004-8).
// Reads are further limited to the doctor's own schedule inside AppointmentService (BR-7, ADR-0012).
[Route("api/v1/appointments")]
[Authorize(Policy = Policies.ScheduleReader)]
public sealed class AppointmentsController(AppointmentService appointments) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("Day schedule of a doctor (clinic-local date), including cancelled appointments.")]
    public Task<IReadOnlyList<AppointmentDto>> DaySchedule([FromQuery] int doctorId, [FromQuery] DateOnly date, CancellationToken ct) =>
        appointments.GetDayScheduleAsync(doctorId, date, ct);

    [HttpGet("{id:guid}", Name = "GetAppointment")]
    public Task<AppointmentDto> Get(Guid id, CancellationToken ct) => appointments.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Policies.FrontDesk)]
    [EndpointSummary("Book a free slot. 409 doctor_conflict / patient_conflict, 422 appointment_in_past / invalid_slot.")]
    public async Task<ActionResult<AppointmentDto>> Book(BookAppointmentRequest request, CancellationToken ct)
    {
        var created = await appointments.BookAsync(request, ct);
        return CreatedAtRoute("GetAppointment", new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.FrontDesk)]
    public Task<AppointmentDto> Cancel(Guid id, CancelAppointmentRequest request, CancellationToken ct) =>
        appointments.CancelAsync(id, request, ct);

    [HttpPost("{id:guid}/reschedule")]
    [Authorize(Policy = Policies.FrontDesk)]
    public Task<AppointmentDto> Reschedule(Guid id, RescheduleAppointmentRequest request, CancellationToken ct) =>
        appointments.RescheduleAsync(id, request, ct);
}
