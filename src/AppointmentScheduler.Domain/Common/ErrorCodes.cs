namespace AppointmentScheduler.Domain.Common;

/// <summary>
/// Every machine-readable error code the API can return (ADR-0015). Clients branch on these, so a value never changes once
/// shipped. Never throw with a string literal — add the code here, under its feature section.
/// </summary>
public static class ErrorCodes
{
    // ── Generic ──────────────────────────────────────────────────────────────────────────────────────
    /// <summary>FluentValidation or domain input rejection. Field messages are in <c>errors</c>.</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    /// <summary>The request could not be read: malformed JSON, wrong types, unparsable query values.</summary>
    public const string BadRequest = "BAD_REQUEST";

    /// <summary>No valid access token.</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>Authenticated, but the role or BR-7 ownership rule denies access.</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>The request asked for an API version this service does not offer (URL segment, <c>X-Api-Version</c> or
    /// <c>api-version</c>).</summary>
    public const string UnsupportedApiVersion = "UNSUPPORTED_API_VERSION";

    /// <summary>The body is not JSON (wrong <c>Content-Type</c>).</summary>
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";

    /// <summary>No route matches the URL.</summary>
    public const string RouteNotFound = "ROUTE_NOT_FOUND";

    /// <summary>The route exists but not for this HTTP method.</summary>
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";

    /// <summary>The catch-all for an unhandled fault. Accompanied by a trace id, never by internals.</summary>
    public const string InternalError = "INTERNAL_ERROR";

    // ── Appointments ─────────────────────────────────────────────────────────────────────────────────
    public const string AppointmentNotFound      = "APPOINTMENT_NOT_FOUND";
    public const string AppointmentInPast        = "APPOINTMENT_IN_PAST";
    public const string AppointmentNotModifiable = "APPOINTMENT_NOT_MODIFIABLE";

    /// <summary>BR-4: the start is not one of the day's generated 30-minute slots.</summary>
    public const string InvalidSlot              = "INVALID_SLOT";

    /// <summary>BR-1: the doctor already has a scheduled appointment overlapping this time.</summary>
    public const string DoctorConflict           = "DOCTOR_CONFLICT";

    /// <summary>BR-2: the patient already has a scheduled appointment overlapping this time, with any doctor.</summary>
    public const string PatientConflict          = "PATIENT_CONFLICT";

    /// <summary>Optimistic concurrency: the appointment changed since it was read.</summary>
    public const string ConcurrencyConflict      = "CONCURRENCY_CONFLICT";

    // ── Doctors / Patients ───────────────────────────────────────────────────────────────────────────
    public const string DoctorNotFound           = "DOCTOR_NOT_FOUND";
    public const string PatientNotFound          = "PATIENT_NOT_FOUND";

    // ── Auth ─────────────────────────────────────────────────────────────────────────────────────────
    public const string InvalidCredentials       = "INVALID_CREDENTIALS";

    /// <summary>Too many failed sign-ins; the account is locked for a while (423).</summary>
    public const string AccountLocked            = "ACCOUNT_LOCKED";

    public const string InvalidRefreshToken      = "INVALID_REFRESH_TOKEN";

    /// <summary>A rotated refresh token was presented again; its whole family was revoked.</summary>
    public const string RefreshTokenReused       = "REFRESH_TOKEN_REUSED";

    // ── Users ────────────────────────────────────────────────────────────────────────────────────────
    public const string UserNotFound             = "USER_NOT_FOUND";
    public const string EmailTaken               = "EMAIL_TAKEN";
}
