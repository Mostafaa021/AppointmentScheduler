using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Doctors;
using AppointmentScheduler.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Application.Common;

/// <summary>
/// Persistence port (ADR-0001). Implementations must translate DB overlap violations into
/// <see cref="Domain.Common.ConflictException"/> on save.
/// </summary>
public interface IAppDbContext
{
    DbSet<Doctor> Doctors { get; }
    DbSet<Patient> Patients { get; }
    DbSet<Appointment> Appointments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
