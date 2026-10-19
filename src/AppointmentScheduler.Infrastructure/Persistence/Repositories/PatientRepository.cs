using AppointmentScheduler.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Infrastructure.Persistence.Repositories;

internal sealed class PatientRepository(AppDbContext db) : IPatientRepository
{
    public Task<bool> ExistsAsync(int id, CancellationToken ct) => db.Patients.AnyAsync(p => p.Id == id, ct);

    public async Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(string? term, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Patients.AsNoTracking();
        if (!string.IsNullOrEmpty(term))
        {
            // Translates to lower(full_name) LIKE '%term%' OR mrn = 'TERM' (MRNs are stored upper-case).
            var lower = term.ToLowerInvariant();
            var mrn = term.ToUpperInvariant();
            query = query.Where(p => p.FullName.ToLower().Contains(lower) || p.Mrn == mrn);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.FullName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}
