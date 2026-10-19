using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Scheduling;

namespace AppointmentScheduler.Application.Appointments;

public sealed record BookAppointmentRequest(int DoctorId, int PatientId, DateTimeOffset StartUtc, string? Reason);

public sealed record RescheduleAppointmentRequest(DateTimeOffset StartUtc);

public sealed record CancelAppointmentRequest(string? Reason);

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
    AppointmentStatus Status,
    string? CancellationReason,
    uint Version)
{
    /// <summary>Requires <see cref="Appointment.Doctor"/> and <see cref="Appointment.Patient"/> to be loaded.</summary>
    public static AppointmentDto From(Appointment a, DaySchedule schedule) => new(
        a.Id, a.DoctorId, a.Doctor!.FullName, a.PatientId, a.Patient!.FullName, a.Patient.Mrn,
        a.StartUtc, a.EndUtc,
        schedule.LocalTimeOf(a.StartUtc).ToString("HH:mm"), schedule.LocalTimeOf(a.EndUtc).ToString("HH:mm"),
        a.Reason, a.Status, a.CancellationReason, a.Version);
}
