using AppointmentScheduler.Domain.Scheduling;

namespace AppointmentScheduler.UnitTests;

internal static class TestClinic
{
    // Europe/London has DST, so offsets differ across the year (ADR-0003).
    public static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public static readonly DaySchedule Schedule = new(London, new TimeOnly(9, 0), new TimeOnly(17, 0), TimeSpan.FromMinutes(30));

    /// <summary>UTC instant of a London wall-clock time.</summary>
    public static DateTimeOffset At(int year, int month, int day, int hour, int minute = 0)
    {
        var local = new DateTime(year, month, day, hour, minute, 0);
        return new DateTimeOffset(local, London.GetUtcOffset(local)).ToUniversalTime();
    }
}
