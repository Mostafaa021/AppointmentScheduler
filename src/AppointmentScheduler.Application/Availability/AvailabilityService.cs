using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Doctors;
using Microsoft.Extensions.Options;

namespace AppointmentScheduler.Application.Availability;

public enum SlotStatus { Free, Booked, Past, PatientBusy }

public sealed record SlotDto(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string LocalStart, SlotStatus Status, Guid? AppointmentId);

public sealed record AvailabilityDto(int DoctorId, DateOnly Date, string TimeZone, IReadOnlyList<SlotDto> Slots);

/// <summary>Spec 001: classifies each slot of a doctor's day. Two appointment queries regardless of slot count.</summary>
public sealed class AvailabilityService(
    IDoctorRepository doctors,
    IAppointmentRepository appointments,
    TimeProvider clock,
    IOptions<ClinicOptions> clinic)
{
    public async Task<AvailabilityDto> GetAsync(int doctorId, DateOnly date, int? patientId, CancellationToken ct)
    {
        if (!await doctors.ExistsAsync(doctorId, ct))
            throw new NotFoundException(ErrorCodes.DoctorNotFound, $"Doctor {doctorId} was not found.");

        var schedule = clinic.Value.ToDaySchedule();
        var (from, to) = schedule.UtcWindowOf(date);
        var doctorBusy = await appointments.ListScheduledForDoctorAsync(doctorId, from, to, ct);
        var patientBusy = patientId is { } pid ? await appointments.ListScheduledForPatientAsync(pid, from, to, ct) : [];

        var now = clock.GetUtcNow();
        var slots = schedule.SlotsFor(date).Select(s =>
        {
            var booked = doctorBusy.FirstOrDefault(a => a.StartUtc < s.EndUtc && a.EndUtc > s.StartUtc);
            var status = booked is not null ? SlotStatus.Booked
                : s.StartUtc <= now ? SlotStatus.Past
                : patientBusy.Any(a => a.StartUtc < s.EndUtc && a.EndUtc > s.StartUtc) ? SlotStatus.PatientBusy
                : SlotStatus.Free;
            return new SlotDto(s.StartUtc, s.EndUtc, s.LocalStart.ToString("HH:mm"), status, booked?.Id);
        }).ToList();

        return new AvailabilityDto(doctorId, date, schedule.TimeZone.Id, slots);
    }
}
