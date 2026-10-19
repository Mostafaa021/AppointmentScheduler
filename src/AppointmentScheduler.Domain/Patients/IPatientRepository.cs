using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Domain.Patients;

/// <summary>Read access to patients (ADR-0016). Full patient CRUD arrives with spec 005 (M4).</summary>
public interface IPatientRepository : IRepository<Patient, int>
{
    /// <summary>
    /// One page of patients ordered by name; <paramref name="term"/> (already trimmed, null = all) matches a case-insensitive
    /// part of the name or the exact MRN. <c>Total</c> counts all matches.
    /// </summary>
    Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(string? term, int page, int pageSize, CancellationToken ct);
}
