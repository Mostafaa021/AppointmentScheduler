using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Doctors;
using AppointmentScheduler.Domain.Patients;

namespace AppointmentScheduler.Domain.Common;

/// <summary>
/// The only way to reach a repository (ADR-0016): they all share this unit's context, and <see cref="SaveChangesAsync"/>
/// commits everything they staged, atomically. One commit per use case, so a change and its audit row succeed or fail
/// together (ADR-0011). Services inject this, never a repository.
/// </summary>
public interface IUnitOfWork
{
    IAppointmentRepository Appointments { get; }

    IDoctorRepository Doctors { get; }

    IPatientRepository Patients { get; }

    IAuditRecordRepository AuditRecords { get; }

    /// <summary>
    /// Saves all staged changes. Database guarantees surface as domain errors: an overlap exclusion violation →
    /// <see cref="ConflictException"/> <c>DOCTOR_CONFLICT</c>/<c>PATIENT_CONFLICT</c>, a stale version →
    /// <c>CONCURRENCY_CONFLICT</c> (ADR-0002, ADR-0004).
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
