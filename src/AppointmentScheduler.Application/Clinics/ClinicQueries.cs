using AppointmentScheduler.Application.Common;
using Microsoft.Extensions.Options;

namespace AppointmentScheduler.Application.Clinics;

/// <param name="TimeZone">IANA zone every clinic time is shown in (e.g. <c>Europe/London</c>).</param>
/// <param name="Today">The clinic's current local date — not the browser's (ADR-0003).</param>
/// <param name="Opens">Opening time, clinic-local <c>HH:mm</c>.</param>
/// <param name="Closes">Closing time, clinic-local <c>HH:mm</c>.</param>
/// <param name="SlotMinutes">Slot length in minutes.</param>
public sealed record ClinicDto(string TimeZone, DateOnly Today, string Opens, string Closes, int SlotMinutes);

/// <summary>
/// The clinic's zone and today's clinic-local date, so the UI opens on the clinic's day whatever zone the browser is in.
/// shortcut: single clinic from config; per-clinic data arrives with spec 007.
/// </summary>
public sealed class ClinicQueries(IOptions<ClinicOptions> clinic, TimeProvider clock)
{
    public ClinicDto Get()
    {
        var options = clinic.Value;
        var schedule = options.ToDaySchedule();
        return new ClinicDto(options.TimeZone, schedule.LocalDateOf(clock.GetUtcNow()),
            options.Open.ToString("HH:mm"), options.Close.ToString("HH:mm"), options.SlotMinutes);
    }
}
