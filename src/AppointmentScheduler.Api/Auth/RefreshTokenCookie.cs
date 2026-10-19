using AppointmentScheduler.Application.Auth;

namespace AppointmentScheduler.Api.Auth;

/// <summary>
/// The rotating refresh-token cookie (spec 004, ADR-0005). HTTP transport only, so it lives in Api rather than Application
/// (which may not reference ASP.NET Core); controllers call it instead of carrying private cookie helpers.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "refresh_token";

    // Scoped to the v1 auth routes so the cookie never travels with ordinary API calls. A future v2 auth route needs its own
    // path (or a version-neutral one) — browsers only send the cookie to this exact prefix.
    private const string Path = "/api/v1/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Append(HttpResponse response, AuthResult result) =>
        response.Cookies.Append(Name, result.RefreshToken, Options(result.RefreshTokenExpiresAt));

    public static void Delete(HttpResponse response) => response.Cookies.Delete(Name, Options(expires: null));

    // Secure in every environment: Chromium/Firefox treat http://localhost as a secure context (spec 004 Design).
    private static CookieOptions Options(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires,
    };
}
