namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// Transport-level error codes: the ones the HTTP pipeline produces itself (unreadable request, unknown route, unsupported
/// version, unexpected fault, and later idempotency and rate limiting). Business codes live in the Domain <c>ErrorCodes</c> and
/// reach the Api only as strings through <c>ApplicationError</c>, so the Api never references Domain (ADR-0015, CLAUDE.md).
/// Clients branch on these values, so a value never changes once shipped.
/// </summary>
public static class ApiErrorCodes
{
    // ── Shared with Domain ErrorCodes (same values; ApiErrorCodesTests enforces it) ──────────────────
    /// <summary>No valid access token (JWT challenge, bodiless 401).</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>Authenticated, but a role policy denies access (JWT forbid, bodiless 403).</summary>
    public const string Forbidden = "FORBIDDEN";

    // ── Request ──────────────────────────────────────────────────────────────────────────────────────
    /// <summary>The request could not be read: malformed JSON, wrong types, unparsable query values.</summary>
    public const string BadRequest = "BAD_REQUEST";

    /// <summary>The request asked for an API version this service does not offer (URL segment, <c>X-Api-Version</c> or
    /// <c>api-version</c>).</summary>
    public const string UnsupportedApiVersion = "UNSUPPORTED_API_VERSION";

    /// <summary>The body is not JSON (wrong <c>Content-Type</c>).</summary>
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";

    // ── Routing ──────────────────────────────────────────────────────────────────────────────────────
    /// <summary>No route matches the URL.</summary>
    public const string RouteNotFound = "ROUTE_NOT_FOUND";

    /// <summary>The route exists but not for this HTTP method.</summary>
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";

    // ── Server ───────────────────────────────────────────────────────────────────────────────────────
    /// <summary>The catch-all for an unhandled fault. Accompanied by a trace id, never by internals.</summary>
    public const string InternalError = "INTERNAL_ERROR";
}
