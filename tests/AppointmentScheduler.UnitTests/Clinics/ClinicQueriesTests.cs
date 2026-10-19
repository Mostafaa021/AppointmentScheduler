using AppointmentScheduler.Application.Clinics;
using AppointmentScheduler.Application.Common;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AppointmentScheduler.UnitTests.Clinics;

/// <summary>ADR-0003 / plan 015: the UI opens on the clinic's day, which can differ from the UTC (or browser) date.</summary>
public class ClinicQueriesTests
{
    private static ClinicDto At(DateTimeOffset utcNow) =>
        new ClinicQueries(Options.Create(new ClinicOptions()), new FakeTimeProvider(utcNow)).Get();

    [Fact]
    public void Today_is_the_clinic_local_date_in_summer_time()
    {
        // 23:30 UTC on 1 July = 00:30 BST on 2 July in London.
        var clinic = At(new DateTimeOffset(2026, 7, 1, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 7, 2), clinic.Today);
        Assert.Equal("Europe/London", clinic.TimeZone);
    }

    [Fact]
    public void Today_matches_utc_in_winter_time()
    {
        // GMT = UTC, so 23:30 UTC on 1 December is still 1 December in London.
        var clinic = At(new DateTimeOffset(2026, 12, 1, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 12, 1), clinic.Today);
    }

    [Fact]
    public void Reports_opening_hours_and_slot_length()
    {
        var clinic = At(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(("09:00", "17:00", 30), (clinic.Opens, clinic.Closes, clinic.SlotMinutes));
    }
}
