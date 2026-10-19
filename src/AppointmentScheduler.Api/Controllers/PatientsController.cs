using AppointmentScheduler.Api.Auth;
using AppointmentScheduler.Application.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Controllers;

[Route("api/v1/patients")]
[Authorize(Policy = Policies.FrontDesk)]
public sealed class PatientsController(LookupQueries queries) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("Search patients by name (contains) or exact MRN, paged (pageSize ≤ 100).")]
    public Task<PagedResult<PatientDto>> Search([FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct) =>
        queries.SearchPatientsAsync(search, page ?? 1, pageSize ?? 20, ct);
}
