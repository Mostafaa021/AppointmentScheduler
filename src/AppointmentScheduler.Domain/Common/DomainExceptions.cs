namespace AppointmentScheduler.Domain.Common;

/// <summary>Base for expected, client-facing failures. <see cref="Code"/> is a stable machine-readable id (e.g. <c>doctor_conflict</c>).</summary>
public abstract class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>A business rule was violated (HTTP 422).</summary>
public sealed class BusinessRuleException(string code, string message) : DomainException(code, message);

/// <summary>The request conflicts with current state, e.g. an overlapping appointment (HTTP 409).</summary>
public sealed class ConflictException(string code, string message) : DomainException(code, message);

/// <summary>A referenced resource does not exist (HTTP 404).</summary>
public sealed class NotFoundException(string code, string message) : DomainException(code, message);

/// <summary>Input failed validation (HTTP 400). <see cref="Errors"/> maps field name to messages.</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : DomainException("validation_failed", "One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public ValidationException(string field, string message) : this(new Dictionary<string, string[]> { [field] = [message] }) { }
}
