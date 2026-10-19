using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Scheduling;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AppointmentScheduler.Application.Appointments;

/// <summary>
/// Book / cancel / reschedule / read. Overlap pre-checks give specific error codes (BR-1, BR-2);
/// the PostgreSQL exclusion constraints remain the guarantee under concurrency (ADR-0002).
/// Order per use case: validate (ADR-0013) → lookups → BR-7 read check (ADR-0012) → rules → one unit-of-work commit incl.
/// audit (ADR-0011, ADR-0016). Data access only through the unit of work's repositories (ADR-0016).
/// </summary>
public sealed class AppointmentService(
    IUnitOfWork unitOfWork,
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

        unitOfWork.Appointments.Add(appointment);
        audit.Record(AuditActions.AppointmentBooked, nameof(Appointment), appointment.Id.ToString());
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(appointment.Id, ct);
    }

    public async Task<AppointmentDto> CancelAsync(Guid id, CancelAppointmentRequest request, CancellationToken ct)
    {
        cancelValidator.ValidateOrThrow(request);
        var appointment = await FindAsync(id, ct);
        appointment.Cancel(request.Reason, clock.GetUtcNow());
        audit.Record(AuditActions.AppointmentCancelled, nameof(Appointment), id.ToString());
        await unitOfWork.SaveChangesAsync(ct);
        return AppointmentDto.From(appointment, _schedule);
    }

    public async Task<AppointmentDto> RescheduleAsync(Guid id, RescheduleAppointmentRequest request, CancellationToken ct)
    {
        rescheduleValidator.ValidateOrThrow(request);
        var appointment = await FindAsync(id, ct);
        appointment.Reschedule(request.StartUtc!.Value, _schedule, clock.GetUtcNow());
        await EnsureNoOverlapAsync(appointment, ct);
        audit.Record(AuditActions.AppointmentRescheduled, nameof(Appointment), id.ToString());
        await unitOfWork.SaveChangesAsync(ct);
        return AppointmentDto.From(appointment, _schedule);
    }

    public async Task<AppointmentDto> GetAsync(Guid id, CancellationToken ct)
    {
        var appointment = await unitOfWork.Appointments.GetByIdReadOnlyAsync(id, ct) ?? throw AppointmentNotFound(id); // 404 before 403 (AC-004-9)
        currentUser.EnsureCanRead(appointment.DoctorId);
        return AppointmentDto.From(appointment, _schedule);
    }

    /// <summary>All appointments (incl. cancelled) of a doctor on a clinic-local date, ordered by start.</summary>
    public async Task<IReadOnlyList<AppointmentDto>> GetDayScheduleAsync(int doctorId, DateOnly date, CancellationToken ct)
    {
        currentUser.EnsureCanRead(doctorId);
        var (from, to) = _schedule.UtcWindowOf(date);
        var items = await unitOfWork.Appointments.ListDayScheduleAsync(doctorId, from, to, ct);
        return [.. items.Select(a => AppointmentDto.From(a, _schedule))];
    }

    private async Task<Appointment> FindAsync(Guid id, CancellationToken ct) =>
        await unitOfWork.Appointments.GetByIdAsync(id, ct) ?? throw AppointmentNotFound(id);

    private static NotFoundException AppointmentNotFound(Guid id) =>
        new(ErrorCodes.AppointmentNotFound, $"Appointment {id} was not found.");

    private async Task EnsureExistsAsync(int doctorId, int patientId, CancellationToken ct)
    {
        if (!await unitOfWork.Doctors.ExistsAsync(doctorId, ct))
            throw new NotFoundException(ErrorCodes.DoctorNotFound, $"Doctor {doctorId} was not found.");
        if (!await unitOfWork.Patients.ExistsAsync(patientId, ct))
            throw new NotFoundException(ErrorCodes.PatientNotFound, $"Patient {patientId} was not found.");
    }

    private async Task EnsureNoOverlapAsync(Appointment a, CancellationToken ct)
    {
        if (await unitOfWork.Appointments.DoctorHasOverlapAsync(a, ct))
            throw AppointmentConflicts.Doctor("pre-check");
        if (await unitOfWork.Appointments.PatientHasOverlapAsync(a, ct))
            throw AppointmentConflicts.Patient("pre-check");
    }
}

/// <summary>Conflict errors shared by the pre-check and the DB-constraint translation, so both return the same code.</summary>
public static class AppointmentConflicts
{
    /// <param name="logDetail">Where the conflict was detected (pre-check or DB constraint); log-only.</param>
    public static ConflictException Doctor(string? logDetail = null) =>
        new(ErrorCodes.DoctorConflict, "The doctor already has an appointment at this time.") { LogDetail = logDetail };

    /// <param name="logDetail">Where the conflict was detected (pre-check or DB constraint); log-only.</param>
    public static ConflictException Patient(string? logDetail = null) =>
        new(ErrorCodes.PatientConflict, "The patient already has an appointment at this time.") { LogDetail = logDetail };

    public static ConflictException Concurrency() =>
        new(ErrorCodes.ConcurrencyConflict, "The appointment was changed by someone else. Reload and try again.");
}
