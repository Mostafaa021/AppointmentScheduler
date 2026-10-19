namespace AppointmentScheduler.Domain.Appointments;

/// <summary>
/// Persistence of the <see cref="Appointment"/> aggregate (ADR-0016). Changes are staged; <c>IUnitOfWork</c> commits them.
/// Methods that return appointments for display load <see cref="Appointment.Doctor"/> and <see cref="Appointment.Patient"/>.
/// </summary>
public interface IAppointmentRepository
{
    void Add(Appointment appointment);

    /// <summary>Tracked (for changes), with doctor and patient; null when missing.</summary>
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Read-only, with doctor and patient; null when missing.</summary>
    Task<Appointment?> GetByIdReadOnlyAsync(Guid id, CancellationToken ct);

    /// <summary>A doctor's appointments (any status) starting in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), by start.</summary>
    Task<IReadOnlyList<Appointment>> ListDayScheduleAsync(int doctorId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);

    /// <summary>The doctor's <c>Scheduled</c> appointments overlapping [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).</summary>
    Task<IReadOnlyList<Appointment>> ListScheduledForDoctorAsync(int doctorId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);

    /// <summary>The patient's <c>Scheduled</c> appointments (any doctor) overlapping [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).</summary>
    Task<IReadOnlyList<Appointment>> ListScheduledForPatientAsync(int patientId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);

    /// <summary>BR-1 pre-check: another <c>Scheduled</c> appointment of the same doctor overlaps <paramref name="appointment"/>.</summary>
    Task<bool> DoctorHasOverlapAsync(Appointment appointment, CancellationToken ct);

    /// <summary>BR-2 pre-check: another <c>Scheduled</c> appointment of the same patient overlaps <paramref name="appointment"/>.</summary>
    Task<bool> PatientHasOverlapAsync(Appointment appointment, CancellationToken ct);
}
