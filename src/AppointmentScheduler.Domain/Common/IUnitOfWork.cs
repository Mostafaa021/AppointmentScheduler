namespace AppointmentScheduler.Domain.Common;

/// <summary>
/// Commits everything the repositories of the current request staged, atomically (ADR-0016). One call per use case, so a
/// change and its audit row succeed or fail together (ADR-0011).
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves all staged changes. Database guarantees surface as domain errors: an overlap exclusion violation →
    /// <see cref="ConflictException"/> <c>DOCTOR_CONFLICT</c>/<c>PATIENT_CONFLICT</c>, a stale version →
    /// <c>CONCURRENCY_CONFLICT</c> (ADR-0002, ADR-0004).
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
