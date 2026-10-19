using AppointmentScheduler.Application.Appointments;
using AppointmentScheduler.Application.Auth;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.UnitTests.Appointments;

public class ScheduleAccessTests
{
    private sealed record User(IReadOnlyCollection<string> Roles, int? DoctorId) : ICurrentUser
    {
        public Guid? UserId => Guid.NewGuid();
        public string? TraceId => null;
    }

    [Fact]
    public void AC_004_9_Doctor_only_user_reads_own_doctor()
    {
        new User([Roles.Doctor], 7).EnsureCanRead(7);
    }

    [Fact]
    public void AC_004_9_Doctor_only_user_is_forbidden_for_another_doctor()
    {
        var ex = Assert.Throws<ForbiddenException>(() => new User([Roles.Doctor], 7).EnsureCanRead(8));

        Assert.Equal(ErrorCodes.Forbidden, ex.ErrorCode);
    }

    [Fact]
    public void AC_004_9_Doctor_only_user_without_doctorId_fails_closed()
    {
        Assert.Throws<ForbiddenException>(() => new User([Roles.Doctor], null).EnsureCanRead(7));
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Receptionist)]
    public void AC_004_8_Roles_are_a_union_so_front_desk_doctors_read_any_schedule(string otherRole)
    {
        new User([otherRole], null).EnsureCanRead(8);
        new User([Roles.Doctor, otherRole], 7).EnsureCanRead(8);
    }
}
