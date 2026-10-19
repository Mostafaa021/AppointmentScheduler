namespace AppointmentScheduler.Domain.Common;

/// <summary>
/// Business error codes thrown by Domain, Application and Infrastructure (ADR-0015). Clients branch on these, so a value never
/// changes once shipped. Never throw with a string literal — add the code here, under its feature section. Transport-level
/// codes the HTTP pipeline produces itself (BAD_REQUEST, ROUTE_NOT_FOUND, INTERNAL_ERROR, …) live in the Api's
/// <c>ApiErrorCodes</c>.
/// </summary>
public static class ErrorCodes
{
    // ── Generic ──────────────────────────────────────────────────────────────────────────────────────
    /// <summary>FluentValidation or domain input rejection. Field messages are in <c>errors</c>.</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    /// <summary>No valid access token. Also in <c>ApiErrorCodes</c> (same value) for the JWT challenge.</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>The role or BR-7 ownership rule denies access. Also in <c>ApiErrorCodes</c> (same value) for the JWT forbid.</summary>
    public const string Forbidden = "FORBIDDEN";

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
