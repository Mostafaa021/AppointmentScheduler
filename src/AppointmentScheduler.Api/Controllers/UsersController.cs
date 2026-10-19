using AppointmentScheduler.Api.Auth;
using AppointmentScheduler.Api.Contracts;
using AppointmentScheduler.Application.Auth;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/users")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class UsersController(IUserAdminService users) : ApiControllerBase
{
    // 201 without Location: there is no GET /users/{id} until S-43.
    [HttpPost]
    [EndpointSummary("Create a user. 400 VALIDATION_FAILED, 404 DOCTOR_NOT_FOUND, 409 EMAIL_TAKEN.")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)] // IDEMPOTENCY_KEY_REUSED
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await users.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<UserDto>.Created(user, "User created."));
    }

    [HttpPut("{id:guid}/roles")]
    [EndpointSummary("Replace a user's roles and doctor link; applies at their next login or refresh. 404 USER_NOT_FOUND.")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetRoles(
        [FromRoute] Guid id,
        [FromBody] SetUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await users.SetRolesAsync(id, request, cancellationToken);
        return OkResponse(user, "Roles updated. They apply at the user's next sign-in or token refresh.");
    }
}
