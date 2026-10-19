namespace AppointmentScheduler.Api.Errors;

/// <summary>
/// Transport-level error codes: the ones the HTTP pipeline produces itself (unreadable request, unknown route, unsupported
/// version, idempotency, rate limiting, unexpected fault, unreachable database). Business codes live in the Domain <c>ErrorCodes</c> and
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

    // ── Idempotency (spec 009) ───────────────────────────────────────────────────────────────────────
    /// <summary>A write without an <c>Idempotency-Key</c> header (or one longer than 100 characters).</summary>
    public const string IdempotencyKeyRequired = "IDEMPOTENCY_KEY_REQUIRED";

    /// <summary>The key was already used for a different method, path or body (422).</summary>
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";

    /// <summary>The first request with this key is still running; retry after <c>Retry-After</c> seconds (409).</summary>
    public const string IdempotencyRequestInProgress = "IDEMPOTENCY_REQUEST_IN_PROGRESS";

    // ── Rate limiting (ADR-0009) ─────────────────────────────────────────────────────────────────
    /// <summary>A rate-limit policy rejected the request; retry after <c>Retry-After</c> seconds (429).</summary>
    public const string RateLimited = "RATE_LIMITED";

    // ── Server ───────────────────────────────────────────────────────────────────────────────────────
    /// <summary>The catch-all for an unhandled fault. Accompanied by a trace id, never by internals.</summary>
    public const string InternalError = "INTERNAL_ERROR";

    /// <summary>The database is unreachable (transient failure). Nothing was half-saved; retry after <c>Retry-After</c>
    /// seconds, with the same <c>Idempotency-Key</c> for writes (503, ADR-0008).</summary>
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
}
