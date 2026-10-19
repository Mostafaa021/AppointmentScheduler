using AppointmentScheduler.Domain.Scheduling;

namespace AppointmentScheduler.Application.Common;

/// <summary>
/// Clinic opening hours, bound from configuration section <c>Clinic</c>.
/// shortcut: single clinic from config; moves to the clinics/working_hours tables in M4 (spec 006/007).
/// </summary>
public sealed class ClinicOptions
{
    public const string Section = "Clinic";

    public string TimeZone { get; set; } = "Europe/London";
    public TimeOnly Open { get; set; } = new(9, 0);
    public TimeOnly Close { get; set; } = new(17, 0);
    public int SlotMinutes { get; set; } = 30;

    public DaySchedule ToDaySchedule() =>
        new(TimeZoneInfo.FindSystemTimeZoneById(TimeZone), Open, Close, TimeSpan.FromMinutes(SlotMinutes));
}
