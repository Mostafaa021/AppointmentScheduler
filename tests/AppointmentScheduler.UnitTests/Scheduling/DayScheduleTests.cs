using AppointmentScheduler.Domain.Common;
using static AppointmentScheduler.UnitTests.TestClinic;

namespace AppointmentScheduler.UnitTests.Scheduling;

public class DayScheduleTests
{
    [Fact]
    public void AC_001_1_Generates_16_half_hour_slots_from_0900_to_1630_local()
    {
        var slots = Schedule.SlotsFor(new DateOnly(2026, 11, 2));

        Assert.Equal(16, slots.Count);
        Assert.Equal(new TimeOnly(9, 0), slots[0].LocalStart);
        Assert.Equal(new TimeOnly(16, 30), slots[^1].LocalStart);
        Assert.All(slots, s => Assert.Equal(TimeSpan.FromMinutes(30), s.EndUtc - s.StartUtc));
    }

    [Fact]
    public void AC_001_6_Slots_are_utc_and_follow_the_clinic_offset_across_dst()
    {
        // London: BST (UTC+1) until 2026-10-25 01:00 UTC, then GMT (UTC+0).
        var beforeDst = Schedule.SlotsFor(new DateOnly(2026, 10, 24))[0];
        var dstDay = Schedule.SlotsFor(new DateOnly(2026, 10, 25))[0];

        Assert.Equal(new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.Zero), beforeDst.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero), dstDay.StartUtc);
        Assert.Equal(TimeSpan.Zero, dstDay.StartUtc.Offset);
        Assert.Equal(16, Schedule.SlotsFor(new DateOnly(2026, 10, 25)).Count);
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(16, 30)]
    public void BR_4_Accepts_slot_starts_inside_hours(int hour, int minute)
    {
        var start = At(2026, 11, 2, hour, minute);

        Assert.Equal(start, Schedule.EnsureValidSlot(start).StartUtc);
    }

    [Theory]
    [InlineData(8, 30)]  // before opening
    [InlineData(17, 0)]  // would end after closing
    [InlineData(10, 15)] // off the 30-minute grid
    public void AC_002_5_Rejects_starts_outside_hours_or_off_grid(int hour, int minute)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Schedule.EnsureValidSlot(At(2026, 11, 2, hour, minute)));

        Assert.Equal(ErrorCodes.InvalidSlot, ex.ErrorCode);
    }

    [Fact]
    public void UtcWindowOf_covers_the_local_day()
    {
        var (start, end) = Schedule.UtcWindowOf(new DateOnly(2026, 7, 1));

        Assert.Equal(new DateTimeOffset(2026, 6, 30, 23, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(TimeSpan.FromHours(24), end - start);
    }
}
