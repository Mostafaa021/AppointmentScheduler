using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Doctors;
using AppointmentScheduler.Domain.Patients;
using AppointmentScheduler.Domain.Scheduling;

namespace AppointmentScheduler.Domain.Appointments;

public enum AppointmentStatus { Scheduled, Cancelled }

/// <summary>
/// Aggregate root. Enforces BR-3 (not in the past), BR-4 (valid slot) and BR-5 (only future Scheduled ones change).
/// Overlaps (BR-1/BR-2) need other rows, so they are checked by the application service and guaranteed by DB constraints.
/// </summary>
public sealed class Appointment : IEntity<Guid>
{
    public const int ReasonMaxLength = 500;

    public Guid Id { get; private set; }
    public int DoctorId { get; private set; }
    public int PatientId { get; private set; }
    public DateTimeOffset StartUtc { get; private set; }
    public DateTimeOffset EndUtc { get; private set; }
    public string Reason { get; private set; } = "";
    public AppointmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    /// <summary>Optimistic concurrency token, mapped to PostgreSQL <c>xmin</c> (ADR-0004).</summary>
    public uint Version { get; private set; }

    public Doctor? Doctor { get; private set; }
    public Patient? Patient { get; private set; }

    private Appointment() { } // EF

    public static Appointment Schedule(int doctorId, int patientId, DateTimeOffset startUtc, string? reason, DaySchedule schedule, DateTimeOffset nowUtc)
    {
        var slot = ValidateNewTime(startUtc, schedule, nowUtc);
        return new Appointment
        {
            Id = Guid.CreateVersion7(),
            DoctorId = doctorId,
            PatientId = patientId,
            StartUtc = slot.StartUtc,
            EndUtc = slot.EndUtc,
            Reason = NormalizeReason(reason),
            Status = AppointmentStatus.Scheduled,
            CreatedAtUtc = nowUtc,
        };
    }

    public void Cancel(string? reason, DateTimeOffset nowUtc)
    {
        EnsureModifiable(nowUtc);
        Status = AppointmentStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : NormalizeReason(reason);
    }

    public void Reschedule(DateTimeOffset newStartUtc, DaySchedule schedule, DateTimeOffset nowUtc)
    {
        EnsureModifiable(nowUtc);
        var slot = ValidateNewTime(newStartUtc, schedule, nowUtc);
        StartUtc = slot.StartUtc;
        EndUtc = slot.EndUtc;
    }

    private void EnsureModifiable(DateTimeOffset nowUtc)
    {
        if (Status != AppointmentStatus.Scheduled || StartUtc <= nowUtc)
            throw new BusinessRuleException(ErrorCodes.AppointmentNotModifiable, "Only future scheduled appointments can be changed.");
    }

    private static Slot ValidateNewTime(DateTimeOffset startUtc, DaySchedule schedule, DateTimeOffset nowUtc)
    {
        if (startUtc <= nowUtc)
            throw new BusinessRuleException(ErrorCodes.AppointmentInPast, "Appointments cannot be booked in the past.");
        return schedule.EnsureValidSlot(startUtc);
    }

    private static string NormalizeReason(string? reason)
    {
        var trimmed = reason?.Trim() ?? "";
        if (trimmed.Length is 0 or > ReasonMaxLength)
            throw new ValidationException("reason", $"Reason is required and must be at most {ReasonMaxLength} characters.");
        return trimmed;
    }
}
