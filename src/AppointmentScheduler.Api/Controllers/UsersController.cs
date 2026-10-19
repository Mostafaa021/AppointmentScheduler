using AppointmentScheduler.Api.Auth;
using AppointmentScheduler.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Controllers;

[Route("api/v1/users")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class UsersController(IUserAdminService users) : ApiControllerBase
{
    // 201 without Location: there is no GET /users/{id} until S-43.
    [HttpPost]
    [EndpointSummary("Create a user. 404 doctor_not_found, 409 email_taken.")]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await users.CreateAsync(request, ct));

    [HttpPut("{id:guid}/roles")]
    [EndpointSummary("Replace a user's roles and doctor link; applies at their next login or refresh.")]
    public Task<UserDto> SetRoles(Guid id, SetUserRolesRequest request, CancellationToken ct) =>
        users.SetRolesAsync(id, request, ct);
}
