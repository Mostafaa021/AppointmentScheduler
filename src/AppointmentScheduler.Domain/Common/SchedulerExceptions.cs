namespace AppointmentScheduler.Domain.Common;

/// <summary>
/// Base for expected, client-facing failures (ADR-0015). <see cref="ErrorCode"/> comes from <see cref="ErrorCodes"/>,
/// <see cref="StatusCode"/> is the HTTP status the API answers with, and <see cref="Exception.Message"/> is safe to show.
/// </summary>
public abstract class SchedulerException : Exception
{
    public string ErrorCode { get; }

    public abstract int StatusCode { get; }

    // Log-only diagnostics: ids, constraint names, states. Never serialised, so the safe Message can stay
    // safe without losing what an engineer needs. Ids only — never names, e-mails or phone numbers (NFR-9).
    public string? LogDetail { get; init; }

    protected SchedulerException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// 400 — input failed validation. <see cref="Errors"/> maps camelCase field name to messages.
/// </summary>
public class ValidationException : SchedulerException
{
    public override int StatusCode => 400;

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(ErrorCodes.ValidationFailed, "One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }
}

/// <summary>
/// 401 — authentication failed: wrong credentials, or a missing/invalid refresh token.
/// </summary>
public class UnauthorizedException : SchedulerException
{
    public override int StatusCode => 401;

    public UnauthorizedException(string errorCode, string message, Exception? innerException = null)
        : base(errorCode, message, innerException) { }
}

/// <summary>
/// 403 — the caller may not access this resource, e.g. BR-7 (a doctor reading another doctor's schedule).
/// </summary>
public class ForbiddenException : SchedulerException
{
    public override int StatusCode => 403;

    public ForbiddenException(string errorCode = ErrorCodes.Forbidden, string message = "You are not allowed to access this resource.")
        : base(errorCode, message) { }
}

/// <summary>
/// 404 — a referenced resource does not exist.
/// </summary>
public class NotFoundException : SchedulerException
{
    public override int StatusCode => 404;

    public NotFoundException(string errorCode, string message, Exception? innerException = null)
        : base(errorCode, message, innerException) { }
}

/// <summary>
/// 409 — the request conflicts with current state, e.g. an overlapping appointment.
/// </summary>
public class ConflictException : SchedulerException
{
    public override int StatusCode => 409;

    public ConflictException(string errorCode, string message, Exception? innerException = null)
        : base(errorCode, message, innerException) { }
}

/// <summary>
/// 422 — the request was understood and valid, but breaks a domain rule (e.g. booking in the past).
/// </summary>
public class BusinessRuleException : SchedulerException
{
    public override int StatusCode => 422;

    public BusinessRuleException(string errorCode, string message, Exception? innerException = null)
        : base(errorCode, message, innerException) { }
}

/// <summary>
/// 423 — the resource is temporarily locked, e.g. an account after repeated failed sign-ins.
/// </summary>
public class LockedException : SchedulerException
{
    public override int StatusCode => 423;

    public LockedException(string errorCode, string message, Exception? innerException = null)
        : base(errorCode, message, innerException) { }
}
