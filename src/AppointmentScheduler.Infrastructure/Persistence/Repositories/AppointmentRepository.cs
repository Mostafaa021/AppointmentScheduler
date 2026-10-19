using AppointmentScheduler.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Infrastructure.Persistence.Repositories;

internal sealed class AppointmentRepository(AppDbContext db) : IAppointmentRepository
{
    public void Add(Appointment appointment) => db.Appointments.Add(appointment);

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        WithPeople(db.Appointments).FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Appointment?> GetByIdReadOnlyAsync(Guid id, CancellationToken ct) =>
        WithPeople(db.Appointments.AsNoTracking()).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Appointment>> ListDayScheduleAsync(
        int doctorId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) =>
        await WithPeople(db.Appointments.AsNoTracking())
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

    private static IQueryable<Appointment> WithPeople(IQueryable<Appointment> query) =>
        query.Include(a => a.Doctor).Include(a => a.Patient);

    // Half-open [start, end) like the exclusion constraints, so back-to-back slots never overlap.
    private IQueryable<Appointment> ScheduledOverlapping(DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Scheduled && a.StartUtc < toUtc && a.EndUtc > fromUtc);

    private IQueryable<Appointment> OthersOverlapping(Appointment a) =>
        ScheduledOverlapping(a.StartUtc, a.EndUtc).Where(o => o.Id != a.Id);
}
