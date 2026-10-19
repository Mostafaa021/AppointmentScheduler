using AppointmentScheduler.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Application.Lookups;

public sealed record DoctorDto(int Id, string FullName, string Specialty);

public sealed record PatientDto(int Id, string Mrn, string FullName, DateOnly DateOfBirth, string Phone);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

/// <summary>Read-only lookups for doctors and patients (spec 005 AC-005-1; full patient CRUD arrives in M4).</summary>
public sealed class LookupQueries(IAppDbContext db)
{
    public const int MaxPageSize = 100;

    public async Task<IReadOnlyList<DoctorDto>> ListDoctorsAsync(CancellationToken ct) =>
        await db.Doctors.AsNoTracking().OrderBy(d => d.FullName)
            .Select(d => new DoctorDto(d.Id, d.FullName, d.Specialty)).ToListAsync(ct);

    public async Task<PagedResult<PatientDto>> SearchPatientsAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Patients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.FullName.ToLower().Contains(term.ToLower()) || p.Mrn == term.ToUpperInvariant());
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.FullName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new PatientDto(p.Id, p.Mrn, p.FullName, p.DateOfBirth, p.Phone)).ToListAsync(ct);
        return new PagedResult<PatientDto>(items, page, pageSize, total);
    }
}
