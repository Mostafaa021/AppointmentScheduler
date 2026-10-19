using AppointmentScheduler.Domain.Doctors;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Infrastructure.Persistence.Repositories;

internal sealed class DoctorRepository(AppDbContext db) : IDoctorRepository
{
    public Task<bool> ExistsAsync(int id, CancellationToken ct) => db.Doctors.AnyAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Doctor>> ListAsync(CancellationToken ct) =>
        await db.Doctors.AsNoTracking().OrderBy(d => d.FullName).ToListAsync(ct);
}
