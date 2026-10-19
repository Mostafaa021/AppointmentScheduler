using AppointmentScheduler.Domain.Doctors;
using AppointmentScheduler.Domain.Patients;

namespace AppointmentScheduler.Application.Lookups;

public sealed record DoctorDto(int Id, string FullName, string Specialty);

public sealed record PatientDto(int Id, string Mrn, string FullName, DateOnly DateOfBirth, string Phone);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

/// <summary>Read-only lookups for doctors and patients (spec 005 AC-005-1; full patient CRUD arrives in M4).</summary>
public sealed class LookupQueries(IDoctorRepository doctors, IPatientRepository patients)
{
    public const int MaxPageSize = 100;

    public async Task<IReadOnlyList<DoctorDto>> ListDoctorsAsync(CancellationToken ct) =>
        [.. (await doctors.ListAsync(ct)).Select(d => new DoctorDto(d.Id, d.FullName, d.Specialty))];

    public async Task<PagedResult<PatientDto>> SearchPatientsAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var (items, total) = await patients.SearchAsync(term, page, pageSize, ct);
        return new PagedResult<PatientDto>(
            [.. items.Select(p => new PatientDto(p.Id, p.Mrn, p.FullName, p.DateOfBirth, p.Phone))], page, pageSize, total);
    }
}
