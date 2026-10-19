using AppointmentScheduler.Api.Auth;
using AppointmentScheduler.Api.Contracts;
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
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PatientDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Search(
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken = default)
    {
        var patients = await queries.SearchPatientsAsync(search, page ?? 1, pageSize ?? 20, cancellationToken);
        return OkResponse(patients);
    }
}
