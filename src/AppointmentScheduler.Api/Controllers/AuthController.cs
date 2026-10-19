using AppointmentScheduler.Api.Contracts;
using AppointmentScheduler.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Controllers;

[Route("api/v1")]
[Tags("Auth")]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    private const string RefreshCookie = "refresh_token";
    private const string RefreshCookiePath = "/api/v1/auth";

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
        return Issue(result, "Signed in.");
    }

    [HttpPost("auth/refresh")]
    [AllowAnonymous]
    [EndpointSummary("Rotate the refresh_token cookie. 401 INVALID_REFRESH_TOKEN / REFRESH_TOKEN_REUSED.")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken = default)
    {
        var result = await auth.RefreshAsync(Request.Cookies[RefreshCookie], cancellationToken);
        return Issue(result, "Session refreshed.");
    }

    // 204 without a body: there is nothing to return, and the cookie deletion is the whole response.
    [HttpPost("auth/logout")]
    [EndpointSummary("Revoke the refresh token and clear the cookie. The access token stays valid until it expires.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookie], cancellationToken);
        Response.Cookies.Delete(RefreshCookie, CookieOptions(expires: null));
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

    private OkObjectResult Issue(AuthResult result, string message)
    {
        Response.Cookies.Append(RefreshCookie, result.RefreshToken, CookieOptions(result.RefreshTokenExpiresAt));
        return OkResponse(result.Response, message);
    }

    // Secure in every environment: Chromium/Firefox treat http://localhost as a secure context (spec 004 Design).
    private static CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
        Expires = expires,
    };
}
