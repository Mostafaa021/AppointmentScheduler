using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Scheduling;

namespace AppointmentScheduler.Application.Appointments;

public sealed record BookAppointmentRequest(int DoctorId, int PatientId, DateTimeOffset? StartUtc, string? Reason);

public sealed record RescheduleAppointmentRequest(DateTimeOffset? StartUtc);

public sealed record CancelAppointmentRequest(string? Reason);

/// <summary>
/// Appointment status as the API contract exposes it. Mirrors the Domain <c>AppointmentStatus</c> by name, so the JSON
/// (<c>"Scheduled"</c> / <c>"Cancelled"</c>) is unchanged while the Api stays free of Domain types.
/// </summary>
public enum AppointmentStatusDto { Scheduled, Cancelled }

public sealed record AppointmentDto(
    Guid Id,
    int DoctorId,
    string DoctorName,
    int PatientId,
    string PatientName,
    string PatientMrn,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string LocalStart,
    string LocalEnd,
    string Reason,
    AppointmentStatusDto Status,
    string? CancellationReason,
    uint Version)
{
    /// <summary>Requires <see cref="Appointment.Doctor"/> and <see cref="Appointment.Patient"/> to be loaded.</summary>
    public static AppointmentDto From(Appointment a, DaySchedule schedule) => new(
        a.Id, a.DoctorId, a.Doctor!.FullName, a.PatientId, a.Patient!.FullName, a.Patient.Mrn,
        a.StartUtc, a.EndUtc,
        schedule.LocalTimeOf(a.StartUtc).ToString("HH:mm"), schedule.LocalTimeOf(a.EndUtc).ToString("HH:mm"),
        a.Reason, ToDto(a.Status), a.CancellationReason, a.Version);

    private static AppointmentStatusDto ToDto(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Scheduled => AppointmentStatusDto.Scheduled,
        AppointmentStatus.Cancelled => AppointmentStatusDto.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped appointment status."),
    };
}
