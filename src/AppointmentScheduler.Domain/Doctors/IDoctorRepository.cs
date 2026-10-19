using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Domain.Doctors;

/// <summary>Read access to doctors (ADR-0016). Doctor management arrives with spec 006.</summary>
public interface IDoctorRepository : IRepository<Doctor, int>
{
    /// <summary>All doctors, ordered by name.</summary>
    Task<IReadOnlyList<Doctor>> ListAsync(CancellationToken ct);
}
