using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppointmentScheduler.Application.Availability;

public enum SlotStatus { Free, Booked, Past, PatientBusy }

public sealed record SlotDto(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string LocalStart, SlotStatus Status, Guid? AppointmentId);

public sealed record AvailabilityDto(int DoctorId, DateOnly Date, string TimeZone, IReadOnlyList<SlotDto> Slots);

/// <summary>Spec 001: classifies each slot of a doctor's day. Two queries regardless of slot count.</summary>
public sealed class AvailabilityService(IAppDbContext db, TimeProvider clock, IOptions<ClinicOptions> clinic)
{
    public async Task<AvailabilityDto> GetAsync(int doctorId, DateOnly date, int? patientId, CancellationToken ct)
    {
        if (!await db.Doctors.AnyAsync(d => d.Id == doctorId, ct))
            throw new NotFoundException("doctor_not_found", $"Doctor {doctorId} was not found.");

        var schedule = clinic.Value.ToDaySchedule();
        var (from, to) = schedule.UtcWindowOf(date);
        var dayScheduled = db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Scheduled && a.StartUtc < to && a.EndUtc > from);

        var doctorBusy = await dayScheduled.Where(a => a.DoctorId == doctorId)
            .Select(a => new { a.Id, a.StartUtc, a.EndUtc }).ToListAsync(ct);
        var patientBusy = patientId is null ? [] : await dayScheduled.Where(a => a.PatientId == patientId)
            .Select(a => new { a.Id, a.StartUtc, a.EndUtc }).ToListAsync(ct);

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
