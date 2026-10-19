using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Scheduling;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppointmentScheduler.Application.Appointments;

/// <summary>
/// Book / cancel / reschedule / read. Overlap pre-checks give specific error codes (BR-1, BR-2);
/// the PostgreSQL exclusion constraints remain the guarantee under concurrency (ADR-0002).
/// Order per use case: validate (ADR-0013) → lookups → BR-7 read check (ADR-0012) → rules → one save incl. audit (ADR-0011).
/// </summary>
public sealed class AppointmentService(
    IAppDbContext db,
    TimeProvider clock,
    IOptions<ClinicOptions> clinic,
    ICurrentUser currentUser,
    AuditLog audit,
    IValidator<BookAppointmentRequest> bookValidator,
    IValidator<CancelAppointmentRequest> cancelValidator,
    IValidator<RescheduleAppointmentRequest> rescheduleValidator)
{
    private readonly DaySchedule _schedule = clinic.Value.ToDaySchedule();

    public async Task<AppointmentDto> BookAsync(BookAppointmentRequest request, CancellationToken ct)
    {
        bookValidator.ValidateOrThrow(request);
        await EnsureExistsAsync(request.DoctorId, request.PatientId, ct);

        var appointment = Appointment.Schedule(
            request.DoctorId, request.PatientId, request.StartUtc!.Value, request.Reason, _schedule, clock.GetUtcNow());
        await EnsureNoOverlapAsync(appointment, ct);

        db.Appointments.Add(appointment);
        audit.Record(AuditActions.AppointmentBooked, nameof(Appointment), appointment.Id.ToString());
        await db.SaveChangesAsync(ct);
        return await GetAsync(appointment.Id, ct);
    }

    public async Task<AppointmentDto> CancelAsync(Guid id, CancelAppointmentRequest request, CancellationToken ct)
    {
        cancelValidator.ValidateOrThrow(request);
        var appointment = await FindAsync(id, ct);
        appointment.Cancel(request.Reason, clock.GetUtcNow());
        audit.Record(AuditActions.AppointmentCancelled, nameof(Appointment), id.ToString());
        await db.SaveChangesAsync(ct);
        return AppointmentDto.From(appointment, _schedule);
    }

    public async Task<AppointmentDto> RescheduleAsync(Guid id, RescheduleAppointmentRequest request, CancellationToken ct)
    {
        rescheduleValidator.ValidateOrThrow(request);
        var appointment = await FindAsync(id, ct);
        appointment.Reschedule(request.StartUtc!.Value, _schedule, clock.GetUtcNow());
        await EnsureNoOverlapAsync(appointment, ct);
        audit.Record(AuditActions.AppointmentRescheduled, nameof(Appointment), id.ToString());
        await db.SaveChangesAsync(ct);
        return AppointmentDto.From(appointment, _schedule);
    }

    public async Task<AppointmentDto> GetAsync(Guid id, CancellationToken ct)
    {
        var appointment = await FindAsync(id, ct, tracked: false); // 404 before 403 (AC-004-9)
        currentUser.EnsureCanRead(appointment.DoctorId);
        return AppointmentDto.From(appointment, _schedule);
    }

    /// <summary>All appointments (incl. cancelled) of a doctor on a clinic-local date, ordered by start.</summary>
    public async Task<IReadOnlyList<AppointmentDto>> GetDayScheduleAsync(int doctorId, DateOnly date, CancellationToken ct)
    {
        currentUser.EnsureCanRead(doctorId);
        var (from, to) = _schedule.UtcWindowOf(date);
        var items = await db.Appointments.AsNoTracking()
            .Include(a => a.Doctor).Include(a => a.Patient)
            .Where(a => a.DoctorId == doctorId && a.StartUtc >= from && a.StartUtc < to)
            .OrderBy(a => a.StartUtc).ThenBy(a => a.Status)
            .ToListAsync(ct);
        return items.ConvertAll(a => AppointmentDto.From(a, _schedule));
    }

    private async Task<Appointment> FindAsync(Guid id, CancellationToken ct, bool tracked = true)
    {
        var query = tracked ? db.Appointments : db.Appointments.AsNoTracking();
        return await query.Include(a => a.Doctor).Include(a => a.Patient).FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("appointment_not_found", $"Appointment {id} was not found.");
    }

    private async Task EnsureExistsAsync(int doctorId, int patientId, CancellationToken ct)
    {
        if (!await db.Doctors.AnyAsync(d => d.Id == doctorId, ct))
            throw new NotFoundException("doctor_not_found", $"Doctor {doctorId} was not found.");
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new NotFoundException("patient_not_found", $"Patient {patientId} was not found.");
    }

    private async Task EnsureNoOverlapAsync(Appointment a, CancellationToken ct)
    {
        var overlapping = db.Appointments.Where(o => o.Id != a.Id && o.Status == AppointmentStatus.Scheduled
                                                     && o.StartUtc < a.EndUtc && o.EndUtc > a.StartUtc);
        if (await overlapping.AnyAsync(o => o.DoctorId == a.DoctorId, ct))
            throw AppointmentConflicts.Doctor();
        if (await overlapping.AnyAsync(o => o.PatientId == a.PatientId, ct))
            throw AppointmentConflicts.Patient();
    }
}

/// <summary>Conflict errors shared by the pre-check and the DB-constraint translation, so both return the same code.</summary>
public static class AppointmentConflicts
{
    public static ConflictException Doctor() =>
        new("doctor_conflict", "The doctor already has an appointment at this time.");

    public static ConflictException Patient() =>
        new("patient_conflict", "The patient already has an appointment at this time.");

    public static ConflictException Concurrency() =>
        new("concurrency_conflict", "The appointment was changed by someone else. Reload and try again.");
}
