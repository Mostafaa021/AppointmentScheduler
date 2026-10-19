using AppointmentScheduler.Api.Auth;
using AppointmentScheduler.Api.Contracts;
using AppointmentScheduler.Application.Auth;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}")]
[Tags("Auth")]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    [HttpPost("auth/login")]
    [AllowAnonymous]
    [EndpointSummary("Sign in. 400 VALIDATION_FAILED, 401 INVALID_CREDENTIALS, 423 ACCOUNT_LOCKED. Sets the refresh_token cookie.")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        RefreshTokenCookie.Append(Response, result);
        return OkResponse(result.Response, "Signed in.");
    }

    [HttpPost("auth/refresh")]
    [AllowAnonymous]
    [EndpointSummary("Rotate the refresh_token cookie. 401 INVALID_REFRESH_TOKEN / REFRESH_TOKEN_REUSED.")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken = default)
    {
        var result = await auth.RefreshAsync(RefreshTokenCookie.Read(Request), cancellationToken);
        RefreshTokenCookie.Append(Response, result);
        return OkResponse(result.Response, "Session refreshed.");
    }

    // 204 without a body: there is nothing to return, and the cookie deletion is the whole response.
    [HttpPost("auth/logout")]
    [EndpointSummary("Revoke the refresh token and clear the cookie. The access token stays valid until it expires.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        await auth.LogoutAsync(RefreshTokenCookie.Read(Request), cancellationToken);
        RefreshTokenCookie.Delete(Response);
        return NoContent();
    }

    [HttpGet("me")]
    [EndpointSummary("The signed-in user.")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken = default)
    {
        var me = await auth.GetMeAsync(cancellationToken);
        return OkResponse(me);
    }
}
