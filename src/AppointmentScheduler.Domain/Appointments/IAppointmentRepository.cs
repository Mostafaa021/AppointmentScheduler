using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Domain.Appointments;

/// <summary>
/// Persistence of the <see cref="Appointment"/> aggregate (ADR-0016). Changes are staged; <c>IUnitOfWork</c> commits them.
/// Methods that return appointments for display, including the inherited get-by-id, load <see cref="Appointment.Doctor"/> and
/// <see cref="Appointment.Patient"/>.
/// </summary>
public interface IAppointmentRepository : IRepository<Appointment, Guid>
{
    /// <summary>A doctor's appointments (any status) starting in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), by start.</summary>
    Task<IReadOnlyList<Appointment>> ListDayScheduleAsync(int doctorId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);

    /// <summary>The doctor's <c>Scheduled</c> appointments overlapping [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).</summary>
    Task<IReadOnlyList<Appointment>> ListScheduledForDoctorAsync(int doctorId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);

    /// <summary>The patient's <c>Scheduled</c> appointments (any doctor) overlapping [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).</summary>
    Task<IReadOnlyList<Appointment>> ListScheduledForPatientAsync(int patientId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);

    /// <summary>
    /// Queues competing bookings: takes transaction-scoped locks on the doctor, then the patient (always that order, so they
    /// can't deadlock). Call inside <c>IUnitOfWork.ExecuteInTransactionAsync</c> before the pre-checks; a second booking
    /// for the same doctor or patient waits for the first to commit, then its pre-check sees it (SPEC §5). The exclusion
    /// constraints stay the guarantee; the lock turns PostgreSQL's deadlocks between conflicting inserts into a clean 409.
    /// </summary>
    Task LockScheduleAsync(int doctorId, int patientId, CancellationToken ct);

    /// <summary>BR-1 pre-check: another <c>Scheduled</c> appointment of the same doctor overlaps <paramref name="appointment"/>.</summary>
    Task<bool> DoctorHasOverlapAsync(Appointment appointment, CancellationToken ct);

    /// <summary>BR-2 pre-check: another <c>Scheduled</c> appointment of the same patient overlaps <paramref name="appointment"/>.</summary>
    Task<bool> PatientHasOverlapAsync(Appointment appointment, CancellationToken ct);
}
