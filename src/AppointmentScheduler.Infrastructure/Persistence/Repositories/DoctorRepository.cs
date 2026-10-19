using AppointmentScheduler.Domain.Doctors;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Infrastructure.Persistence.Repositories;

internal sealed class DoctorRepository(AppDbContext db) : Repository<Doctor, int>(db), IDoctorRepository
{
    public async Task<IReadOnlyList<Doctor>> ListAsync(CancellationToken ct) =>
        await Set.AsNoTracking().OrderBy(d => d.FullName).ToListAsync(ct);
}
