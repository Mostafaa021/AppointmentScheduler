using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;
using static AppointmentScheduler.UnitTests.TestClinic;

namespace AppointmentScheduler.UnitTests.Appointments;

public class AppointmentTests
{
    private static readonly DateTimeOffset Now = At(2026, 11, 2, 10, 5);

    private static Appointment Book(DateTimeOffset start, string reason = "Annual check-up") =>
        Appointment.Schedule(doctorId: 1, patientId: 2, start, reason, Schedule, Now);

    [Fact]
    public void AC_002_1_Schedules_a_30_minute_appointment_with_trimmed_reason()
    {
        var a = Book(At(2026, 11, 2, 11, 0), "  Follow-up  ");

        Assert.Equal(AppointmentStatus.Scheduled, a.Status);
        Assert.Equal(TimeSpan.FromMinutes(30), a.EndUtc - a.StartUtc);
        Assert.Equal("Follow-up", a.Reason);
        Assert.Equal(Now, a.CreatedAtUtc);
        Assert.NotEqual(Guid.Empty, a.Id);
    }

    [Theory]
    [InlineData(10, 0)] // slot already started (now = 10:05)
    [InlineData(9, 0)]
    public void AC_002_4_Rejects_booking_in_the_past(int hour, int minute)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Book(At(2026, 11, 2, hour, minute)));

        Assert.Equal("appointment_in_past", ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AC_002_6_Requires_a_reason(string? reason)
    {
        var ex = Assert.Throws<ValidationException>(() => Book(At(2026, 11, 2, 11, 0), reason!));

        Assert.Contains("reason", ex.Errors.Keys);
    }

    [Fact]
    public void AC_002_6_Rejects_reason_longer_than_500()
    {
        Assert.Throws<ValidationException>(() => Book(At(2026, 11, 2, 11, 0), new string('x', 501)));
    }

    [Fact]
    public void AC_003_2_Cancel_marks_cancelled()
    {
        var a = Book(At(2026, 11, 2, 11, 0));

        a.Cancel(" Patient called ", Now);

        Assert.Equal(AppointmentStatus.Cancelled, a.Status);
        Assert.Equal(Now, a.CancelledAtUtc);
        Assert.Equal("Patient called", a.CancellationReason);
    }

    [Fact]
    public void AC_003_3_Cannot_cancel_twice()
    {
        var a = Book(At(2026, 11, 2, 11, 0));
        a.Cancel(null, Now);

        var ex = Assert.Throws<BusinessRuleException>(() => a.Cancel(null, Now));

        Assert.Equal("appointment_not_modifiable", ex.Code);
    }

    [Fact]
    public void AC_003_3_Cannot_change_an_appointment_that_already_started()
    {
        var a = Book(At(2026, 11, 2, 11, 0));
        var later = At(2026, 11, 2, 11, 10);

        Assert.Throws<BusinessRuleException>(() => a.Cancel(null, later));
        Assert.Throws<BusinessRuleException>(() => a.Reschedule(At(2026, 11, 3, 9, 0), Schedule, later));
    }

    [Fact]
    public void AC_003_4_Reschedule_moves_to_new_slot()
    {
        var a = Book(At(2026, 11, 2, 11, 0));

        a.Reschedule(At(2026, 11, 3, 14, 30), Schedule, Now);

        Assert.Equal(At(2026, 11, 3, 14, 30), a.StartUtc);
        Assert.Equal(At(2026, 11, 3, 15, 0), a.EndUtc);
    }

    [Fact]
    public void AC_003_6_Reschedule_rejects_past_and_invalid_slots()
    {
        var a = Book(At(2026, 11, 2, 11, 0));

        Assert.Equal("appointment_in_past",
            Assert.Throws<BusinessRuleException>(() => a.Reschedule(At(2026, 11, 2, 9, 30), Schedule, Now)).Code);
        Assert.Equal("invalid_slot",
            Assert.Throws<BusinessRuleException>(() => a.Reschedule(At(2026, 11, 3, 18, 0), Schedule, Now)).Code);
    }
}
