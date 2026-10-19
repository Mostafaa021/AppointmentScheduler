using AppointmentScheduler.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Infrastructure.Persistence.Repositories;

internal sealed class AppointmentRepository(AppDbContext db) : Repository<Appointment, Guid>(db), IAppointmentRepository
{
    public async Task<IReadOnlyList<Appointment>> ListDayScheduleAsync(
        int doctorId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) =>
        await WithDetails(Set.AsNoTracking())
            .Where(a => a.DoctorId == doctorId && a.StartUtc >= fromUtc && a.StartUtc < toUtc)
            .OrderBy(a => a.StartUtc).ThenBy(a => a.Status)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> ListScheduledForDoctorAsync(
        int doctorId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) =>
        await ScheduledOverlapping(fromUtc, toUtc).Where(a => a.DoctorId == doctorId).ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> ListScheduledForPatientAsync(
        int patientId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) =>
        await ScheduledOverlapping(fromUtc, toUtc).Where(a => a.PatientId == patientId).ToListAsync(ct);

    public Task<bool> DoctorHasOverlapAsync(Appointment appointment, CancellationToken ct) =>
        OthersOverlapping(appointment).AnyAsync(o => o.DoctorId == appointment.DoctorId, ct);

    public Task<bool> PatientHasOverlapAsync(Appointment appointment, CancellationToken ct) =>
        OthersOverlapping(appointment).AnyAsync(o => o.PatientId == appointment.PatientId, ct);

    // Appointments are shown with their doctor and patient names.
    protected override IQueryable<Appointment> WithDetails(IQueryable<Appointment> query) =>
        query.Include(a => a.Doctor).Include(a => a.Patient);

    // Half-open [start, end) like the exclusion constraints, so back-to-back slots never overlap.
    private IQueryable<Appointment> ScheduledOverlapping(DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        Set.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Scheduled && a.StartUtc < toUtc && a.EndUtc > fromUtc);

    private IQueryable<Appointment> OthersOverlapping(Appointment a) =>
        ScheduledOverlapping(a.StartUtc, a.EndUtc).Where(o => o.Id != a.Id);
}
