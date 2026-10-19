using AppointmentScheduler.Api.Auth;
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
    public Task<IReadOnlyList<DoctorDto>> List(CancellationToken ct) => queries.ListDoctorsAsync(ct);

    [HttpGet("{id:int}/availability")]
    [EndpointSummary("Slots of a doctor's day: Free, Booked, Past or PatientBusy (when patientId is given).")]
    public Task<AvailabilityDto> Availability(int id, [FromQuery] DateOnly date, [FromQuery] int? patientId, CancellationToken ct) =>
        availability.GetAsync(id, date, patientId, ct);
}
