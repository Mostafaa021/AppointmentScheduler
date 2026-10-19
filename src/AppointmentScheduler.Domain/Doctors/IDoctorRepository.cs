namespace AppointmentScheduler.Domain.Doctors;

/// <summary>Read access to doctors (ADR-0016). Doctor management arrives with spec 006.</summary>
public interface IDoctorRepository
{
    Task<bool> ExistsAsync(int id, CancellationToken ct);

    /// <summary>All doctors, ordered by name.</summary>
    Task<IReadOnlyList<Doctor>> ListAsync(CancellationToken ct);
}
