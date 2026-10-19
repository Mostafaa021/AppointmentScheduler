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
    [EndpointSummary("Sign in. 400 validation_failed, 401 invalid_credentials, 423 account_locked. Sets the refresh_token cookie.")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Issue(await auth.LoginAsync(request, ct));

    [HttpPost("auth/refresh")]
    [AllowAnonymous]
    [EndpointSummary("Rotate the refresh_token cookie. 401 invalid_refresh_token / refresh_token_reused.")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct) =>
        Issue(await auth.RefreshAsync(Request.Cookies[RefreshCookie], ct));

    [HttpPost("auth/logout")]
    [EndpointSummary("Revoke the refresh token and clear the cookie. The access token stays valid until it expires.")]
    public async Task<NoContentResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookie], ct);
        Response.Cookies.Delete(RefreshCookie, CookieOptions(expires: null));
        return NoContent();
    }

    [HttpGet("me")]
    public Task<UserDto> Me(CancellationToken ct) => auth.GetMeAsync(ct);

    private OkObjectResult Issue(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookie, result.RefreshToken, CookieOptions(result.RefreshTokenExpiresAt));
        return Ok(result.Response);
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
