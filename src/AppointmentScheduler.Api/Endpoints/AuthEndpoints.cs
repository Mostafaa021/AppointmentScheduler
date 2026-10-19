using AppointmentScheduler.Application.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AppointmentScheduler.Api.Endpoints;

internal static class AuthEndpoints
{
    private const string RefreshCookie = "refresh_token";
    private const string RefreshCookiePath = "/api/v1/auth";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/login", async (LoginRequest request, IAuthService service, HttpResponse response, CancellationToken ct) =>
                Issue(response, await service.LoginAsync(request, ct)))
            .AllowAnonymous()
            .WithSummary("Sign in. 400 validation_failed, 401 invalid_credentials, 423 account_locked. Sets the refresh_token cookie.");

        auth.MapPost("/refresh", async (HttpContext http, IAuthService service, CancellationToken ct) =>
                Issue(http.Response, await service.RefreshAsync(http.Request.Cookies[RefreshCookie], ct)))
            .AllowAnonymous()
            .WithSummary("Rotate the refresh_token cookie. 401 invalid_refresh_token / refresh_token_reused.");

        auth.MapPost("/logout", async (HttpContext http, IAuthService service, CancellationToken ct) =>
            {
                await service.LogoutAsync(http.Request.Cookies[RefreshCookie], ct);
                http.Response.Cookies.Delete(RefreshCookie, CookieOptions(expires: null));
                return TypedResults.NoContent();
            })
            .WithSummary("Revoke the refresh token and clear the cookie. The access token stays valid until it expires.");

        api.MapGet("/me", (IAuthService service, CancellationToken ct) => service.GetMeAsync(ct)).WithTags("Auth");

        return api;
    }

    private static Ok<AuthResponse> Issue(HttpResponse response, AuthResult result)
    {
        response.Cookies.Append(RefreshCookie, result.RefreshToken, CookieOptions(result.RefreshTokenExpiresAt));
        return TypedResults.Ok(result.Response);
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
